# Imagen de la API para hostings con contenedores (Render). Se construye desde la carpeta backend/.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacion
WORKDIR /src
COPY GrupoJuridico.Gestion.sln ./
COPY src/GrupoJuridico.Gestion.Domain/*.csproj src/GrupoJuridico.Gestion.Domain/
COPY src/GrupoJuridico.Gestion.Application/*.csproj src/GrupoJuridico.Gestion.Application/
COPY src/GrupoJuridico.Gestion.Infrastructure/*.csproj src/GrupoJuridico.Gestion.Infrastructure/
COPY src/GrupoJuridico.Gestion.API/*.csproj src/GrupoJuridico.Gestion.API/
RUN dotnet restore src/GrupoJuridico.Gestion.API/GrupoJuridico.Gestion.API.csproj
COPY src/ src/
RUN dotnet publish src/GrupoJuridico.Gestion.API/GrupoJuridico.Gestion.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=compilacion /app ./
# El hosting define PORT; sin él escucha en 8080. Corre con el usuario sin privilegios de la imagen.
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GrupoJuridico.Gestion.API.dll"]
