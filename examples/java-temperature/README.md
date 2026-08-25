# Java temperature driver

This simulated driver mirrors the C# example and demonstrates annotations,
typed Function parameters, progress/cancellation, all device member types, and
a robot-accessible Nest.

```bash
./mvnw -pl examples/java-temperature -am install
./mvnw -f examples/java-temperature/pom.xml exec:java
```

Then open `http://127.0.0.1:18081/Info`.

Validate the same YAML without creating the driver, connecting hardware, or opening a port:

```bash
./mvnw -f examples/java-temperature/pom.xml exec:java -Dexec.args="--validate"
```
