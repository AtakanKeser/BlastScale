# ---------- build stage: compile and package with Maven ----------
FROM maven:3.9-eclipse-temurin-21-alpine AS build
WORKDIR /workspace

# Resolve dependencies first so this (slow) layer is cached until pom.xml changes.
COPY pom.xml .
RUN mvn -B -q dependency:go-offline

COPY src ./src
# Tests run in CI (./mvnw verify with Testcontainers); the image build only packages.
RUN mvn -B -q -DskipTests package

# ---------- runtime stage: small JRE image, non-root user ----------
FROM eclipse-temurin:21-jre-alpine
RUN addgroup -S blastscale && adduser -S blastscale -G blastscale
WORKDIR /app
COPY --from=build /workspace/target/blastscale-backend-*.jar app.jar
USER blastscale
EXPOSE 8080

# JAVA_TOOL_OPTIONS (heap sizing, GC) is injected by docker-compose.
ENTRYPOINT ["java", "-jar", "app.jar"]
