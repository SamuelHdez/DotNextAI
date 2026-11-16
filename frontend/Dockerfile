# syntax=docker/dockerfile:1
# Web app image. Build context: frontend/ (docker build frontend).
# Base image pinned by digest; Dependabot keeps tag and digest up to date.

FROM node:24-alpine@sha256:116b800c3754b440ee0aa3ed2f20f5bf3d99a554bfd9adeea09ea8017f6e85f5 AS base

FROM base AS deps
WORKDIR /app
COPY package.json package-lock.json ./
# Lifecycle scripts are a common malware vector in compromised packages.
RUN npm ci --ignore-scripts

FROM base AS build
WORKDIR /app

# NEXT_PUBLIC_* values are inlined into the client bundle at build time.
ARG NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_API_BASE_URL=${NEXT_PUBLIC_API_BASE_URL} \
    NEXT_TELEMETRY_DISABLED=1

COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM base AS runner
WORKDIR /app

# The standalone server only needs `node`. Package managers bundled in the base image are removed to shrink the
# attack surface (and their vendored dependencies, e.g. npm's tar/undici, are flagged by image scanners).
RUN rm -rf /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/corepack /opt/yarn-v* \
    /usr/local/bin/npm /usr/local/bin/npx /usr/local/bin/corepack /usr/local/bin/yarn /usr/local/bin/yarnpkg

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3030 \
    HOSTNAME=0.0.0.0

COPY --from=build --chown=node:node /app/public ./public
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static

USER node
EXPOSE 3030
CMD ["node", "server.js"]
