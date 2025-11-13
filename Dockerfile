# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy solution/project files first (speeds up cache)
COPY *.sln ./
COPY **/*.csproj ./
RUN dotnet restore

# copy all source and publish
COPY . .
RUN dotnet publish -c Release -o /app/publish

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Render sẽ forward traffic tới port mà container lắng nghe
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

COPY --from=build /app/publish ./

# ENTRYPOINT: đổi tên DLL thành tên app bạn cung cấp
ENTRYPOINT ["dotnet", "ExamGradingApp.dll"]
