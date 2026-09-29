#!/usr/bin/env bash
#
# Validates the committed JSON Schemas with the sourcemeta jsonschema CLI (ADR-0011): each schema
# must satisfy its own meta-schema and this repo's lint rules, and every conforming instance
# document must validate against schemas/netpc.v1.schema.json. Exits non-zero on the first failure.
#
#   eng/validate-schemas.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCHEMA="$REPO_ROOT/schemas/netpc.v1.schema.json"
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

echo "validate-schemas.sh: metaschema $SCHEMA"
"${JSONSCHEMA[@]}" metaschema "$SCHEMA" || fail "schema does not satisfy its own meta-schema"

echo "validate-schemas.sh: lint $SCHEMA"
"${JSONSCHEMA[@]}" lint "$SCHEMA" "${LINT_EXCLUDE[@]}" || fail "lint found issues (see above)"

mapfile -t INSTANCES < <(git ls-files '*.netpc.json')
[[ ${#INSTANCES[@]} -gt 0 ]] || fail "no tracked .netpc.json instances found"

for instance in "${INSTANCES[@]}"; do
    echo "validate-schemas.sh: validate $instance"
    "${JSONSCHEMA[@]}" validate "$SCHEMA" "$REPO_ROOT/$instance" || fail "$instance failed schema validation"
done

echo "validate-schemas.sh: all checks passed (${#INSTANCES[@]} instance(s))"
