// Pure helper: turns a feature (or saved-feature) object into display-ready [label, value] pairs.
// No DOM access and no state, so it can be unit-tested on its own.

// Extract useful metadata pairs from a feature object.
// Prioritizes a small set of friendly fields (Name, Feature ID, Region, Capital, Population (est.), Area, Admission)
// Omits any ObjectID/OBJECTID/ObjectId keys and maps common GIS field names to user-friendly labels.
export function extractMetadataPairs(feature) {
    const pairs = [];
    if (!feature) return pairs;

    const pushIf = (label, val) => {
        if (val !== undefined && val !== null && String(val).trim() !== "") {
            pairs.push([label, val]);
        }
    };

    pushIf("Name", feature.displayName || feature.Name || feature.name);
    pushIf("Feature ID", feature.featureId || feature.FeatureId);

    const sources = [feature || {}, feature.properties || {}, feature.attributes || {}];

    const lookup = (keys) => {
        for (const key of keys) {
            for (const s of sources) {
                if (!s) continue;
                // direct match
                if (key in s) return s[key];
                // case-insensitive match
                const found = Object.keys(s).find(k => k.toLowerCase() === key.toLowerCase());
                if (found) return s[found];
            }
        }
        return undefined;
    };

    const keyLabelMap = {
        "sub_region": "Region",
        "region": "Region",
        "state_name": "State",
        "STATE_NAME": "State",
        "capital": "Capital",
        "population": "Population (est.)",
        "pop": "Population (est.)",
        "shape_area": "Area",
        "shape_length": "Perimeter",
        "area": "Area",
        "admission_date": "Admission",
        "state_fips": "FIPS",
        "state_code": "State Code",
        "abbr": "Abbreviation"
    };

    // Preferred keys (order matters)
    const preferredKeyGroups = [
        ["sub_region", "region", "SUB_REGION", "REGION"],
        ["capital", "CAPITAL"],
        ["population", "POP", "pop"],
        ["shape_area", "area", "SHAPE_AREA", "AREA"],
        ["admission_date", "ADMISSION_DATE"]
    ];

    // Push mapped preferred fields first
    for (const group of preferredKeyGroups) {
        const val = lookup(group);
        const exampleKey = group[0];
        const label = keyLabelMap[exampleKey.toLowerCase()] || (group[0] === "population" ? "Population (est.)" : undefined);
        pushIf(label || group[0], val);
    }

    pushIf("Short Description", feature.shortDescription || feature.description || feature.Description);

    // Fallback: include a few additional primitive properties (max 6),
    // but exclude object id fields and already displayed fields.
    const seenLabels = new Set(pairs.map(p => p[0].toLowerCase()));
    const extras = [];
    const propertySources = [feature.properties || {}, feature.attributes || {}, feature];

    const excludedKeysLower = new Set([
        "displayname", "name", "featureid", "feature_id", "description", "shortdescription",
        "objectid", "object_id"
    ]);

    const toFriendlyLabel = (k) => {
        const lower = k.toLowerCase();
        if (keyLabelMap[lower]) return keyLabelMap[lower];
        // convert snake/upper to Title Case: STATE_FIPS -> State Fips -> FIPS override handled above
        return k.replace(/_/g, " ").replace(/\b\w+/g, s => s.charAt(0).toUpperCase() + s.slice(1).toLowerCase());
    };

    for (const src of propertySources) {
        for (const k of Object.keys(src || {})) {
            const kLower = String(k).toLowerCase();
            if (excludedKeysLower.has(kLower)) continue;
            const label = toFriendlyLabel(k);
            if (seenLabels.has(label.toLowerCase())) continue;
            const v = src[k];
            if (v === undefined || v === null) continue;
            // skip large objects, stringify small objects
            if (typeof v === "object") {
                try {
                    const s = JSON.stringify(v);
                    if (s.length > 200) continue;
                    extras.push([label, s]);
                } catch {
                    continue;
                }
            } else {
                extras.push([label, v]);
            }
            seenLabels.add(label.toLowerCase());
            if (extras.length >= 6) break;
        }
        if (extras.length >= 6) break;
    }

    extras.forEach(p => pairs.push(p));

    return pairs;
}
