FROM mcr.microsoft.com/playwright:v1.62.0-noble

WORKDIR /work

COPY package.json package-lock.json ./
RUN npm ci

COPY playwright.config.js ./
COPY tests/e2e ./tests/e2e

ENTRYPOINT ["npm", "run", "test:e2e", "--"]
