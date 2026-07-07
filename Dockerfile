# ═══════════════════════════════════════════════════════════════════
# Dockerfile چندمرحله‌ای برای CapRover
#   ۱) build جزیره‌های Vue با Node → wwwroot/dist
#   ۲) publish اپ ASP.NET و جای‌گذاری dist در خروجی
#   ۳) ایمیج runtime سبک که روی پورت 80 گوش می‌دهد (CapRover SSL را مدیریت می‌کند)
# ═══════════════════════════════════════════════════════════════════

# ---------- مرحلهٔ ۱: فرانت ----------
FROM node:22-alpine AS frontend
WORKDIR /build/ClientApp
COPY src/RugsManagment.Web/ClientApp/package.json src/RugsManagment.Web/ClientApp/package-lock.json ./
RUN npm ci
COPY src/RugsManagment.Web/ClientApp/ ./
RUN npm run build
# خروجی build در /build/wwwroot/dist قرار می‌گیرد

# ---------- مرحلهٔ ۲: بک‌اند ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/RugsManagment.Web/RugsManagment.Web.csproj -c Release -o /publish /p:UseAppHost=false
# جزیره‌های build‌شده را داخل wwwroot خروجی publish کپی کن
COPY --from=frontend /build/wwwroot/dist /publish/wwwroot/dist

# ---------- مرحلهٔ ۳: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
COPY --from=backend /publish ./
ENTRYPOINT ["dotnet", "RugsManagment.Web.dll"]
