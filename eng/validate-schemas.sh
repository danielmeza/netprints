#!/usr/bin/env bash
#
# Validates the committed JSON Schemas with the sourcemeta jsonschema CLI (ADR-0011): each schema
# must satisfy its own meta-schema and this repo's lint rules, and every tracked instance document
# (*.netpc.json against netpc.v1, *.npcat.json against npcat.v1) must validate against its schema.
# Exits non-zero on the first failure.
#
#   eng/validate-schemas.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
JSONSCHEMA=(mise exec -- jsonschema)

# mise resolves mise.toml (and the pinned jsonschema version) from the current directory upward.
cd "$REPO_ROOT"

fail() {
    echo "validate-schemas.sh: FAIL: $1" >&2
    exit 1
}

# $schema and $kind are the wire format's real property names (document-format.md §1.1, §1.4), not a
# naming convention the schema can change; excluded rather than fixed (ADR-0011). Embedding an example
# document at the schema's top level would duplicate the fixtures this script already validates below,
# and drift from them since this file is generated, never hand-edited (ADR-0011).
LINT_EXCLUDE=(--exclude simple_properties_identifiers --exclude top_level_examples)

# Each schema and the glob of the instance files it governs.
SCHEMAS=(
    "netpc.v1.schema.json:*.netpc.json"
    "npcat.v1.schema.json:*.npcat.json"
    "netprints.catalog.v1.schema.json::(glob)**/netprints.catalog.json"
)

total=0
for entry in "${SCHEMAS[@]}"; do
    schema="$REPO_ROOT/schemas/${entry%%:*}"
    glob="${entry#*:}"

    echo "validate-schemas.sh: metaschema $schema"
    "${JSONSCHEMA[@]}" metaschema "$schema" || fail "$schema does not satisfy its own meta-schema"

    echo "validate-schemas.sh: lint $schema"
    "${JSONSCHEMA[@]}" lint "$schema" "${LINT_EXCLUDE[@]}" || fail "lint found issues in $schema (see above)"

    mapfile -t INSTANCES < <(git ls-files "$glob")
    [[ ${#INSTANCES[@]} -gt 0 ]] || fail "no tracked $glob instances found"

    for instance in "${INSTANCES[@]}"; do
        echo "validate-schemas.sh: validate $instance"
        "${JSONSCHEMA[@]}" validate "$schema" "$REPO_ROOT/$instance" || fail "$instance failed schema validation"
        total=$((total + 1))
    done
done

echo "validate-schemas.sh: all checks passed ($total instance(s))"
