# Build context: repository root (needs libs/go). Example:
#   docker build -f services/inventory/Dockerfile .
FROM golang:1.26 AS build
WORKDIR /src
COPY libs/go ./libs/go
COPY services/inventory ./services/inventory
WORKDIR /src/services/inventory
RUN CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o /out/inventory ./cmd/inventory

FROM gcr.io/distroless/static-debian12:nonroot
COPY --from=build /out/inventory /inventory
USER nonroot
ENTRYPOINT ["/inventory"]
