# Dockerfile (publish by solution)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy everything (preserves folder structure)
COPY . .

# restore using solution
RUN dotnet restore "ExamGradingApp.sln"

# publish entire solution
RUN dotnet publish "ExamGradingApp.sln" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ExamGradingApp.dll"]
