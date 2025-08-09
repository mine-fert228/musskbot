# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY telegrambotwin/telegrambotwin.csproj ./
RUN dotnet restore

COPY telegrambotwin/. ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out .

CMD ["dotnet", "telegrambotwin.dll"]
