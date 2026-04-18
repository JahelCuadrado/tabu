using Tabu.Domain.Entities;

namespace Tabu.Domain.Interfaces;

public interface ISettingsRepository
{
    UserSettings Load();
    void Save(UserSettings settings);
}
