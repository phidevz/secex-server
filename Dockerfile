#
#  ---------------------------------------------------------------------
#  secex - Secure File Exchange                                         
#  Copyright (c) 2022  phidevz                                          
#                                                                       
#  This program is free software: you can redistribute it and/or modify 
#  it under the terms of the GNU General Public License as published by 
#  the Free Software Foundation, either version 3 of the License, or    
#  (at your option) any later version.                                  
#                                                                       
#  This program is distributed in the hope that it will be useful,      
#  but WITHOUT ANY WARRANTY; without even the implied warranty of       
#  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the        
#  GNU General Public License for more details.                         
#                                                                       
#  You should have received a copy of the GNU General Public License    
#  along with this program. If not, see <https://www.gnu.org/licenses/>.
#  ---------------------------------------------------------------------
#

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
WORKDIR /app
EXPOSE 80
ENV Logging__LogLevel__Microsoft__AspNetCore=Warning

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["secex-server.csproj", "./"]
RUN dotnet restore "secex-server.csproj"
COPY Program.cs .
COPY Endpoints.cs .
WORKDIR "/src/"
RUN dotnet build "secex-server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "secex-server.csproj" -c Release -o /app/publish

FROM base AS final
ENV SECEX_ENABLE_BROWSE_FILES=false
WORKDIR /app
COPY --from=publish /app/publish .
WORKDIR /app/store
VOLUME /app/store
ENTRYPOINT ["dotnet", "/app/secex-server.dll"]
