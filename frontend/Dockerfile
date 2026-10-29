# Build context is this frontend/ folder (see docker-compose.yml).

FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install

COPY . .

# Vite bakes VITE_* variables into the JS bundle AT BUILD TIME — this runs in the
# user's BROWSER, not inside Docker's network, so it must be a URL the browser can
# actually reach (never a Docker service name like "http://app:8080"). Passed as a
# build arg so the same Dockerfile works for both local compose and an Azure deploy
# with a different backend URL, without editing any source file.
ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
