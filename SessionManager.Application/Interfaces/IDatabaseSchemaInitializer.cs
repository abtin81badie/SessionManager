namespace SessionManager.Application.Interfaces;

public interface IDatabaseSchemaInitializer
{
    Task EnsureDatabaseCreatedAsync();
}