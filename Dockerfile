FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ExamGradingApp.csproj", "./"]
RUN dotnet restore "ExamGradingApp.csproj"
COPY . .
RUN dotnet build "ExamGradingApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExamGradingApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy tessdata nếu cần cho OCR
COPY tessdata ./tessdata

ENTRYPOINT ["dotnet", "ExamGradingApp.dll"]