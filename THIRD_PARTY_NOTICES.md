# Third-party notices

CIAI uses the following direct runtime libraries. They remain the property of their respective authors and are distributed under their own licenses. Transitive dependencies are recorded by NuGet and Maven package metadata.

| Component | Used by | License |
|---|---|---|
| Microsoft.Extensions.Logging | .NET SDK | MIT |
| System.IO.Ports | .NET SDK | MIT |
| System.Text.Json | .NET SDK | MIT |
| YamlDotNet | .NET SDK | MIT |
| Jackson 2.x | Java SDK | Apache-2.0 |
| SLF4J | Java SDK | MIT |
| SnakeYAML | Java SDK | Apache-2.0 |
| jSerialComm | Java SDK | Apache-2.0 or LGPL-3.0; CIAI uses the Apache-2.0 option |

JUnit Jupiter is used for tests under EPL-2.0. Maven and its build plugins are build tools and are not bundled into the SDK artifacts. The Maven Wrapper scripts are provided by the Apache Maven Wrapper project under Apache-2.0.

Project links and exact resolved versions are available in `CiaiControllerSDK/CiaiControllerSDK.csproj`, `CiaiControllerSDKForJava/pom.xml`, and the package lock/asset metadata produced during a build.
