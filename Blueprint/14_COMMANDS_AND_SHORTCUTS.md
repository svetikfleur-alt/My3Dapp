# Commands and Shortcuts

## Main execution command

### `run blueprint`
Meaning:
Execute the next unfinished Blueprint segment.

## Optional explicit version

### `run blueprint:S-03`
Meaning:
Execute exactly segment S-03 and nothing else.

## Optional review command

### `review blueprint`
Meaning:
Check current implementation against Blueprint and report drift.

## Optional freeze command

### `freeze blueprint`
Meaning:
Do not expand features; only stabilize current architecture.

## Optional plan command

### `show next segment`
Meaning:
Report the next executable segment without implementing it.

## Command interpretation rule

Commands must be interpreted conservatively.
If command scope is ambiguous, prefer smaller safe action.
