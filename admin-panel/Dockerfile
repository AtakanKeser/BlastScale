# ---------------------------------------------------------------------------
# Stage 1: build the static bundle. Vite inlines VITE_* variables at build time,
# so the API / Prometheus URLs are passed as build args (see docker-compose.yml).
# ---------------------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

# Dependencies first so this layer is cached while the sources change.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

ARG VITE_API_BASE_URL=http://localhost:8080
ARG VITE_PROMETHEUS_URL=http://localhost:9090
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL} \
    VITE_PROMETHEUS_URL=${VITE_PROMETHEUS_URL}

COPY . .
RUN npm run build

# ---------------------------------------------------------------------------
# Stage 2: serve dist/ with nginx. nginx.conf adds the SPA fallback so client-side
# routes such as /players/42 survive a full page reload.
# ---------------------------------------------------------------------------
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
