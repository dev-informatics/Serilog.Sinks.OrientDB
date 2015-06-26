# Serilog.Sinks.OrientDB
[![Build status](https://ci.appveyor.com/api/projects/status/1chwti46dk8812rb/branch/master?svg=true)](https://ci.appveyor.com/project/psibernetic/serilog-sinks-orientdb/branch/master)

##Usage
Add to your LoggingConfiguration:
```csharp
.WriteTo.OrientDB("http://server:port", "databaseName", "user", "password")
```
We recommend a new database for logging instead of using a preexisting one as there are database level requirements.

The database that you provide via databaseName should already exist and two ALTER DATABASE commands need to be ran:
````SQL
ALTER DATABASE DATETIMEFORMAT yyyy-MM-dd'T'HH:mm:ss.SSS
ALTER DATABASE TIMEZONE UTC
```

The sink will automatically define an appropriate class (LogEvent by default) with appropriate schema.
