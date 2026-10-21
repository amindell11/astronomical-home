# shellcheck shell=bash
# The board protocol of the "Astronomical" user project (doc/agents/issue-tracker.md § Projects
# board sync): its ids, and the GraphQL reads and writes a board writer makes. Sourced, never run.
# Every call runs under GH_TOKEN as the caller sets it, which needs the `project` scope
# (PROJECTS_TOKEN in Actions), and returns gh's status. Text goes in as a GraphQL variable, so any
# text is safe to pass.

BOARD_PROJECT_ID="PVT_kwHOAJsCkc4BfiTv"
BOARD_DECISION_FIELD_ID="PVTF_lAHOAJsCkc4BfiTvzhkWGNc"

# board_item <owner/repo> <number> <text field name>: sets BOARD_NODE (the issue's or PR's node
# id), BOARD_ITEM (its item on the project; empty when it is not on the board) and BOARD_TEXT (the
# field's value there; empty when unset).
board_item() {
  local out lines
  out="$(gh api graphql -f owner="${1%%/*}" -f name="${1##*/}" -F number="$2" -f field="$3" -f query='
    query($owner: String!, $name: String!, $number: Int!, $field: String!) {
      repository(owner: $owner, name: $name) { issueOrPullRequest(number: $number) {
        ... on Issue { id projectItems(first: 50) { nodes { id project { id }
          fieldValueByName(name: $field) { ... on ProjectV2ItemFieldTextValue { text } } } } }
        ... on PullRequest { id projectItems(first: 50) { nodes { id project { id }
          fieldValueByName(name: $field) { ... on ProjectV2ItemFieldTextValue { text } } } } } } } }' \
    --jq '.data.repository.issueOrPullRequest as $c
      | ([$c.projectItems.nodes[] | select(.project.id == "'"$BOARD_PROJECT_ID"'")][0] // {}) as $i
      | $c.id, ($i.id // ""), ($i.fieldValueByName.text // "")')" || return
  mapfile -t lines <<<"$out"
  BOARD_NODE="${lines[0]}" BOARD_ITEM="${lines[1]:-}" BOARD_TEXT="${lines[2]:-}"
}

# board_texts <owner/repo> <text field name>: the numbers of <owner/repo>'s issues and PRs whose
# item has the field set, one per line.
board_texts() {
  local cursor="" out more
  while :; do
    out="$(gh api graphql -f project="$BOARD_PROJECT_ID" -f field="$2" ${cursor:+-f cursor="$cursor"} -f query='
      query($project: ID!, $field: String!, $cursor: String) { node(id: $project) { ... on ProjectV2 {
        items(first: 100, after: $cursor) { pageInfo { hasNextPage endCursor } nodes {
          fieldValueByName(name: $field) { ... on ProjectV2ItemFieldTextValue { text } }
          content { ... on Issue { number repository { nameWithOwner } }
                    ... on PullRequest { number repository { nameWithOwner } } } } } } } }' \
      --jq '.data.node.items | "\(.pageInfo.hasNextPage) \(.pageInfo.endCursor)",
        (.nodes[] | select((.fieldValueByName.text // "") != "" and .content.repository.nameWithOwner == "'"$1"'")
        | .content.number)')" || return
    read -r more cursor <<<"$out"
    tail -n +2 <<<"$out" | grep . || true
    [[ "$more" == true ]] || return 0
  done
}

# board_add <node id>: adds the issue or PR to the project; prints its item id.
board_add() {
  gh api graphql -f project="$BOARD_PROJECT_ID" -f content="$1" -f query='
    mutation($project: ID!, $content: ID!) {
      addProjectV2ItemById(input: {projectId: $project, contentId: $content}) { item { id } } }' \
    --jq .data.addProjectV2ItemById.item.id
}

# board_set_text <item id> <field id> <text>
board_set_text() {
  gh api graphql -f project="$BOARD_PROJECT_ID" -f item="$1" -f field="$2" -f text="$3" -f query='
    mutation($project: ID!, $item: ID!, $field: ID!, $text: String!) {
      updateProjectV2ItemFieldValue(input: {projectId: $project, itemId: $item, fieldId: $field,
        value: {text: $text}}) { projectV2Item { id } } }' --silent
}

# board_clear <item id> <field id>
board_clear() {
  gh api graphql -f project="$BOARD_PROJECT_ID" -f item="$1" -f field="$2" -f query='
    mutation($project: ID!, $item: ID!, $field: ID!) {
      clearProjectV2ItemFieldValue(input: {projectId: $project, itemId: $item, fieldId: $field}) {
        projectV2Item { id } } }' --silent
}
