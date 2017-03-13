del *.nupkg
nuget pack EntityFramework.LazyLoadLoggingInterceptor.csproj
nuget push *.nupkg -Source https://www.nuget.org/api/v2/package