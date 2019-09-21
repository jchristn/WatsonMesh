# Change Log

## Current Version

v2.0.3

- XML documentation
- Update to latest WatsonTcp (again for XML documentation)

## Previous Versions

v2.0.2

- Dependency updates to better support graceful handling of disconnects

v2.0.1

- Bugfixes

v2.0.0

- Breaking changes!  Task-based callbacks
- Simplified constructors and APIs

v1.2.x

- Added support for sync messaging using streams

v1.1.x

- Added support for sending streams in async messages to support larger messages
- Sync messages (expecting a response) still use byte arrays, as these are usually smaller, interactive messages
- Default constructor for Peer
- Bugfixes and minor refactor

v1.0.x

- Retarget to support .NET Core 2.0 and .NET Framework 4.6.1
- WarningMessage function, which can be useful for sending warning messages to the consuming application.  Useful for debugging issues in particular with synchronous messaging.
- Sync message API (awaits and returns response within specified timeout)
- Initial release

