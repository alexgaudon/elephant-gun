# Use the official .NET 8.0 SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory
WORKDIR /app

# Copy the project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the source code
COPY . ./

# Build the application
RUN dotnet build -c Release -o out

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Install cowsay and add it to PATH
RUN apt-get update && apt-get install -y cowsay && rm -rf /var/lib/apt/lists/*
ENV PATH="/usr/games:${PATH}"

# Set the working directory
WORKDIR /app

# Copy the built application from the build stage
COPY --from=build /app/out ./

# Expose port (optional, for health checks)
EXPOSE 8080

# Set the entry point
ENTRYPOINT ["dotnet", "ElephantGun.dll"] 