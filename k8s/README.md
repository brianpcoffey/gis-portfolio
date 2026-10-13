# Kubernetes Deployment

## Prerequisites
- kubectl configured for your cluster
- Docker image built and pushed to a registry

## Deploy order

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
cp k8s/secret.example.yaml k8s/secret.yaml   # gitignored; fill in real values, then:
kubectl apply -f k8s/secret.yaml
kubectl apply -f k8s/networkpolicy.yaml
kubectl apply -f k8s/redis.yaml
# Set the real image tag first: edit `image: portfolio:REPLACE_ME` in k8s/deployment.yaml,
# or run `kubectl -n <namespace> set image deployment/<name> portfolio=<registry>/portfolio:<tag>` after applying.
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/hpa.yaml
```

The NetworkPolicies assume an ingress controller in a namespace named `ingress-nginx` and CoreDNS in `kube-system` (NodeLocal DNSCache needs an extra egress rule); adjust for your cluster.

## Redis connection string for in-cluster Redis

```
Redis__ConnectionString=redis:6379,password=<REDIS_PASSWORD>,abortConnect=false
```

## Scaling

The HPA scales between 2 and 10 replicas based on CPU (70%) and
memory (80%) utilization. Geocoding cache and job state are shared
via Redis so all replicas serve any request correctly.

## Health endpoints

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Liveness — process is up |
| `GET /health/ready` | Readiness — PostgreSQL and Redis dependencies healthy |

## Secrets management

`k8s/secret.example.yaml` contains only placeholder values; the real `k8s/secret.yaml` is gitignored. Never commit real credentials.
In production use [Sealed Secrets](https://sealed-secrets.netlify.app/) or
[External Secrets Operator](https://external-secrets.io/) to inject secrets from
a vault (AWS Secrets Manager, Azure Key Vault, HashiCorp Vault, etc.).

## Data Protection keys

With `PersistKeysToStackExchangeRedis` configured in `Program.cs`, all pods share
a single key ring stored in Redis under the `DataProtection-Keys` key. The
`/app/DataProtection-Keys` volume mount in `deployment.yaml` uses `emptyDir`
intentionally — it is not the source of truth for key storage.

## In-cluster Redis vs managed Redis

`k8s/redis.yaml` is suitable for staging and demo environments. For production,
replace it with a managed service:
- **Upstash** — serverless Redis with a free tier
- **AWS ElastiCache** — managed Redis in a VPC
- **Azure Cache for Redis** — PaaS Redis on Azure
- **Google Cloud Memorystore** — managed Redis on GCP
