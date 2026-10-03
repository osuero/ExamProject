# syntax=docker/dockerfile:1
# Single image: Angular build served by the ASP.NET Core API (one origin, so cookies and CSRF stay same-site).
FROM node:24.21.0-bookworm-slim AS web
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --ignore-scripts
COPY frontend/ ./
RUN npx ng build --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS api
WORKDIR /src/backend
COPY backend/global.json backend/ExamPrep.sln ./
COPY backend/src/ExamPrep.Api/ExamPrep.Api.csproj src/ExamPrep.Api/
RUN dotnet restore src/ExamPrep.Api/ExamPrep.Api.csproj
COPY backend/src/ ./src/
RUN dotnet publish src/ExamPrep.Api/ExamPrep.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd --system app && useradd --system --gid app --home /app app && mkdir -p /keys && chown app:app /keys
COPY --from=api /out ./
COPY --from=web /src/frontend/dist/frontend ./wwwroot
# Question banks with answer keys stay server-side; they are not part of wwwroot.
COPY content/ /content/
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    Content__Directory=/content \
    DataProtection__KeysPath=/keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExamPrep.Api.dll"]
