FROM docker.indefidev.ir/indefi-back-base:1.0.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM docker.indefidev.ir/indefi-back-base:1.0.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ApiMocker/ApiMocker.csproj", "ApiMocker/"]
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "ApiMocker/ApiMocker.csproj"
COPY . .
WORKDIR "/src/ApiMocker"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet build "ApiMocker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "ApiMocker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiMocker.dll"]