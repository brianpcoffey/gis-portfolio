#include "network_trace_kernel.h"

#include <unordered_map>
#include <vector>

namespace
{
	constexpr int kDeviceSwitch = 1;
	constexpr int kDeviceFuse = 2;
	constexpr int kDeviceRecloser = 3;
	constexpr int kDeviceBreaker = 4;
	constexpr int kDeviceTieSwitch = 5;

	constexpr int kUnvisited = -2;
	constexpr int kSweepOrigin = -1;

	// Node ids are arbitrary integers, so they are densified once into 0..nodeCount-1 and
	// the incidence lists stored as CSR (offsets plus a flat element-index array). Each
	// element also caches the dense index of both endpoints, so the traversals below run
	// entirely on integer array indexing with no hashing in the inner loop.
	//
	// This matters: a map-of-vectors adjacency with a hash lookup per edge measured
	// slower than the managed fallback it was supposed to accelerate.
	struct Graph
	{
		std::unordered_map<int, int> nodeIndex;
		std::vector<int> offsets;
		std::vector<int> incidence;
		std::vector<int> fromDense;
		std::vector<int> toDense;
		int nodeCount = 0;

		int DenseOf(int nodeId) const
		{
			// Never operator[] — a missing node must not silently materialise an entry
			// and, with it, a phantom node in the traversal.
			const auto it = nodeIndex.find(nodeId);
			return it == nodeIndex.end() ? -1 : it->second;
		}

		int OtherEnd(int elementIndex, int denseNode) const
		{
			return fromDense[static_cast<std::size_t>(elementIndex)] == denseNode
				? toDense[static_cast<std::size_t>(elementIndex)]
				: fromDense[static_cast<std::size_t>(elementIndex)];
		}
	};

	Graph build_graph(const TraceElementNative* elements, int elementCount)
	{
		Graph graph;
		graph.nodeIndex.reserve(static_cast<std::size_t>(elementCount) * 2);
		graph.fromDense.resize(static_cast<std::size_t>(elementCount));
		graph.toDense.resize(static_cast<std::size_t>(elementCount));

		for (int i = 0; i < elementCount; ++i)
		{
			const auto from = graph.nodeIndex.emplace(elements[i].fromNodeId, graph.nodeCount);
			if (from.second)
				++graph.nodeCount;
			graph.fromDense[static_cast<std::size_t>(i)] = from.first->second;

			const auto to = graph.nodeIndex.emplace(elements[i].toNodeId, graph.nodeCount);
			if (to.second)
				++graph.nodeCount;
			graph.toDense[static_cast<std::size_t>(i)] = to.first->second;
		}

		graph.offsets.assign(static_cast<std::size_t>(graph.nodeCount) + 1, 0);
		for (int i = 0; i < elementCount; ++i)
		{
			++graph.offsets[static_cast<std::size_t>(graph.fromDense[static_cast<std::size_t>(i)]) + 1];
			++graph.offsets[static_cast<std::size_t>(graph.toDense[static_cast<std::size_t>(i)]) + 1];
		}
		for (int n = 0; n < graph.nodeCount; ++n)
			graph.offsets[static_cast<std::size_t>(n) + 1] += graph.offsets[static_cast<std::size_t>(n)];

		graph.incidence.assign(static_cast<std::size_t>(elementCount) * 2, 0);
		std::vector<int> cursor(graph.offsets.begin(), graph.offsets.end() - 1);

		// Filling in element order keeps each node's incidence list sorted by element
		// index, which is what makes the traversal order reproducible by the managed
		// fallback.
		for (int i = 0; i < elementCount; ++i)
		{
			graph.incidence[static_cast<std::size_t>(cursor[static_cast<std::size_t>(graph.fromDense[static_cast<std::size_t>(i)])]++)] = i;
			graph.incidence[static_cast<std::size_t>(cursor[static_cast<std::size_t>(graph.toDense[static_cast<std::size_t>(i)])]++)] = i;
		}

		return graph;
	}

	int find_element_index(const TraceElementNative* elements, int elementCount, int elementId)
	{
		for (int i = 0; i < elementCount; ++i)
		{
			if (elements[i].id == elementId)
				return i;
		}
		return -1;
	}

	bool is_protective(int deviceType)
	{
		return deviceType == kDeviceFuse || deviceType == kDeviceRecloser || deviceType == kDeviceBreaker;
	}

	bool is_switch(int deviceType)
	{
		return deviceType == kDeviceSwitch || deviceType == kDeviceTieSwitch;
	}

	// Breadth-first sweep from dense node `startDense` over closed elements, recording
	// for every reached node the element index that reached it. `blockedIndex` is never
	// traversed (pass -1 for none). When `visitOrder` is non-null it receives the
	// traversed element indices in discovery order.
	void sweep(
		const TraceElementNative* elements,
		const Graph& graph,
		int startDense,
		int blockedIndex,
		std::vector<int>& reachedBy,
		std::vector<int>* visitOrder)
	{
		reachedBy.assign(static_cast<std::size_t>(graph.nodeCount), kUnvisited);
		if (startDense < 0)
			return;

		std::vector<int> queue;
		queue.reserve(static_cast<std::size_t>(graph.nodeCount));
		queue.push_back(startDense);
		reachedBy[static_cast<std::size_t>(startDense)] = kSweepOrigin;

		for (std::size_t head = 0; head < queue.size(); ++head)
		{
			const int node = queue[head];
			const int begin = graph.offsets[static_cast<std::size_t>(node)];
			const int end = graph.offsets[static_cast<std::size_t>(node) + 1];

			for (int slot = begin; slot < end; ++slot)
			{
				const int index = graph.incidence[static_cast<std::size_t>(slot)];
				if (index == blockedIndex || elements[index].isOpen != 0)
					continue;

				const int next = graph.OtherEnd(index, node);
				if (reachedBy[static_cast<std::size_t>(next)] != kUnvisited)
					continue;

				reachedBy[static_cast<std::size_t>(next)] = index;
				if (visitOrder)
					visitOrder->push_back(index);
				queue.push_back(next);
			}
		}
	}
}

extern "C" TRACE_API int Trace_Downstream(
	const TraceElementNative* elements, int elementCount,
	int sourceNodeId,
	int faultElementId,
	int* outElementIds, int outputCapacity,
	int* outCustomersAffected)
{
	try
	{
		(void)sourceNodeId;

		if (!elements || !outElementIds || !outCustomersAffected)
			return -1;
		if (elementCount <= 0 || outputCapacity < 0)
			return -1;

		const int faultIndex = find_element_index(elements, elementCount, faultElementId);
		if (faultIndex < 0)
			return -5;

		const Graph graph = build_graph(elements, elementCount);

		// Downstream is "the side of the fault away from the source": a sweep from the
		// fault's toNode that is forbidden from crossing back through the fault itself.
		std::vector<int> reachedBy;
		std::vector<int> visitOrder;
		sweep(elements, graph, graph.toDense[static_cast<std::size_t>(faultIndex)], faultIndex, reachedBy, &visitOrder);

		const int total = 1 + static_cast<int>(visitOrder.size());
		if (outputCapacity < total)
			return -3;

		int customers = elements[faultIndex].customerCount;
		outElementIds[0] = elements[faultIndex].id;
		for (std::size_t i = 0; i < visitOrder.size(); ++i)
		{
			const TraceElementNative& element = elements[visitOrder[i]];
			outElementIds[i + 1] = element.id;
			customers += element.customerCount;
		}

		*outCustomersAffected = customers;
		return total;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" TRACE_API int Trace_Upstream(
	const TraceElementNative* elements, int elementCount,
	int sourceNodeId,
	int faultElementId,
	int* outElementIds, int outputCapacity)
{
	try
	{
		if (!elements || !outElementIds)
			return -1;
		if (elementCount <= 0 || outputCapacity < 0)
			return -1;

		const int faultIndex = find_element_index(elements, elementCount, faultElementId);
		if (faultIndex < 0)
			return -5;

		const Graph graph = build_graph(elements, elementCount);

		// Sweeping outward from the source leaves every reached node tagged with the
		// element that energises it; walking those tags back from the fault yields the
		// upstream path without a second search.
		std::vector<int> reachedBy;
		sweep(elements, graph, graph.DenseOf(sourceNodeId), faultIndex, reachedBy, nullptr);

		std::vector<int> path;
		path.push_back(elements[faultIndex].id);

		int node = graph.fromDense[static_cast<std::size_t>(faultIndex)];
		while (node >= 0)
		{
			const int index = reachedBy[static_cast<std::size_t>(node)];
			if (index < 0)
				break;

			path.push_back(elements[index].id);
			node = graph.OtherEnd(index, node);
		}

		const int total = static_cast<int>(path.size());
		if (outputCapacity < total)
			return -3;

		for (int i = 0; i < total; ++i)
			outElementIds[i] = path[i];

		return total;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" TRACE_API int Trace_FindIsolationDevices(
	const TraceElementNative* elements, int elementCount,
	int sourceNodeId,
	int faultElementId,
	int* outDeviceIds, int outputCapacity)
{
	try
	{
		if (!elements || !outDeviceIds)
			return -1;
		if (elementCount <= 0 || outputCapacity < 0)
			return -1;

		const int faultIndex = find_element_index(elements, elementCount, faultElementId);
		if (faultIndex < 0)
			return -5;

		const Graph graph = build_graph(elements, elementCount);
		std::vector<int> devices;

		// Upstream side: the first protective device between the fault and the source.
		// That is the device that actually clears the fault, so it is the one to open.
		std::vector<int> reachedBy;
		sweep(elements, graph, graph.DenseOf(sourceNodeId), faultIndex, reachedBy, nullptr);

		int node = graph.fromDense[static_cast<std::size_t>(faultIndex)];
		while (node >= 0)
		{
			const int index = reachedBy[static_cast<std::size_t>(node)];
			if (index < 0)
				break;

			if (is_protective(elements[index].deviceType))
			{
				devices.push_back(elements[index].id);
				break;
			}
			node = graph.OtherEnd(index, node);
		}

		// Downstream side: the frontier of closed switches. Traversal stops at each one,
		// so only the switches that actually bound the de-energised section are returned
		// — opening every switch downstream would fragment the feeder and leave nothing
		// for a tie to pick up.
		std::vector<char> seenElement(static_cast<std::size_t>(elementCount), 0);
		seenElement[static_cast<std::size_t>(faultIndex)] = 1;

		std::vector<char> visited(static_cast<std::size_t>(graph.nodeCount), 0);
		std::vector<int> queue;

		const int startDense = graph.toDense[static_cast<std::size_t>(faultIndex)];
		visited[static_cast<std::size_t>(startDense)] = 1;
		queue.push_back(startDense);

		for (std::size_t head = 0; head < queue.size(); ++head)
		{
			const int current = queue[head];
			const int begin = graph.offsets[static_cast<std::size_t>(current)];
			const int end = graph.offsets[static_cast<std::size_t>(current) + 1];

			for (int slot = begin; slot < end; ++slot)
			{
				const int index = graph.incidence[static_cast<std::size_t>(slot)];
				if (seenElement[static_cast<std::size_t>(index)])
					continue;
				seenElement[static_cast<std::size_t>(index)] = 1;

				const TraceElementNative& element = elements[index];
				if (element.isOpen != 0)
					continue;

				if (is_switch(element.deviceType))
				{
					devices.push_back(element.id);
					continue; // frontier: do not traverse past it
				}

				const int next = graph.OtherEnd(index, current);
				if (visited[static_cast<std::size_t>(next)])
					continue;

				visited[static_cast<std::size_t>(next)] = 1;
				queue.push_back(next);
			}
		}

		const int total = static_cast<int>(devices.size());
		if (outputCapacity < total)
			return -3;

		for (int i = 0; i < total; ++i)
			outDeviceIds[i] = devices[i];

		return total;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" TRACE_API int Trace_ComputeEnergizedSet(
	const TraceElementNative* elements, int elementCount,
	int sourceNodeId,
	const int* overrideElementIds,
	const int* overrideStates,
	int overrideCount,
	int* outEnergizedElementIds, int outputCapacity,
	int* outCustomersServed)
{
	try
	{
		if (!elements || !outEnergizedElementIds || !outCustomersServed)
			return -1;
		if (elementCount <= 0 || outputCapacity < 0 || overrideCount < 0)
			return -1;
		if (overrideCount > 0 && (!overrideElementIds || !overrideStates))
			return -1;

		// Overrides are a handful of switching operations, so a linear scan per override
		// beats rebuilding or indexing the element array.
		std::vector<char> open(static_cast<std::size_t>(elementCount), 0);
		for (int i = 0; i < elementCount; ++i)
			open[static_cast<std::size_t>(i)] = elements[i].isOpen != 0 ? 1 : 0;

		for (int o = 0; o < overrideCount; ++o)
		{
			const int index = find_element_index(elements, elementCount, overrideElementIds[o]);
			if (index < 0)
				return -2;
			open[static_cast<std::size_t>(index)] = overrideStates[o] != 0 ? 1 : 0;
		}

		const Graph graph = build_graph(elements, elementCount);

		std::vector<char> visited(static_cast<std::size_t>(graph.nodeCount), 0);
		std::vector<int> queue;
		queue.reserve(static_cast<std::size_t>(graph.nodeCount));

		const int startDense = graph.DenseOf(sourceNodeId);
		if (startDense >= 0)
		{
			visited[static_cast<std::size_t>(startDense)] = 1;
			queue.push_back(startDense);
		}

		for (std::size_t head = 0; head < queue.size(); ++head)
		{
			const int node = queue[head];
			const int begin = graph.offsets[static_cast<std::size_t>(node)];
			const int end = graph.offsets[static_cast<std::size_t>(node) + 1];

			for (int slot = begin; slot < end; ++slot)
			{
				const int index = graph.incidence[static_cast<std::size_t>(slot)];
				if (open[static_cast<std::size_t>(index)])
					continue;

				const int next = graph.OtherEnd(index, node);
				if (visited[static_cast<std::size_t>(next)])
					continue;

				visited[static_cast<std::size_t>(next)] = 1;
				queue.push_back(next);
			}
		}

		// A closed element is energised when either endpoint is energised. Collecting in
		// input order (rather than in BFS discovery order) also picks up an element that
		// closes a loop — a closed tie between two live sections carries current even
		// though it reaches no new node.
		int total = 0;
		int customers = 0;
		for (int i = 0; i < elementCount; ++i)
		{
			if (open[static_cast<std::size_t>(i)])
				continue;
			if (!visited[static_cast<std::size_t>(graph.fromDense[static_cast<std::size_t>(i)])] &&
				!visited[static_cast<std::size_t>(graph.toDense[static_cast<std::size_t>(i)])])
				continue;

			if (outputCapacity <= total)
				return -3;

			outEnergizedElementIds[total] = elements[i].id;
			customers += elements[i].customerCount;
			++total;
		}

		*outCustomersServed = customers;
		return total;
	}
	catch (...)
	{
		return -99;
	}
}
