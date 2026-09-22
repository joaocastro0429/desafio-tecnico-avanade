FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src ./src
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ARG PROJECT
WORKDIR /app
COPY --from=build /app/publish .
ENV APP_DLL=${PROJECT}.dll
ENV ASPNETCORE_URLS=http://+:8080
RUN mkdir /data && chown app:app /data
USER app
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
