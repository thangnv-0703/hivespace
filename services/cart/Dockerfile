# Build context: repository root (needs libs/go). Example:
#   docker build -f services/cart/Dockerfile .
FROM golang:1.26 AS build
WORKDIR /src
COPY libs/go ./libs/go
COPY services/cart ./services/cart
WORKDIR /src/services/cart
RUN CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o /out/cart ./cmd/cart

FROM gcr.io/distroless/static-debian12:nonroot
COPY --from=build /out/cart /cart
USER nonroot
ENTRYPOINT ["/cart"]
