using System.Text.Json;

namespace StardewModdingAPI
{
    internal static class Context
    {
        public static bool IsWorldReady { get; set; }
        public static bool IsMainPlayer { get; set; }
    }
    internal enum LogLevel { Info }
    internal interface IMonitor { void Log(string message, LogLevel level); }
    internal interface IManifest { string UniqueID { get; } }
    internal interface IDataHelper
    {
        T? ReadSaveData<T>(string key) where T : class;
        void WriteSaveData<T>(string key, T value);
    }
    internal interface IModHelper { IDataHelper Data { get; } }
    internal sealed class AccessTestHelper : IModHelper, IDataHelper, IManifest, IMonitor
    {
        public string UniqueID => "Test.Workers";
        public IDataHelper Data => this;
        public readonly Dictionary<string, string> Store = new();
        public int Writes { get; private set; }
        public T? ReadSaveData<T>(string key) where T : class
        {
            if (!Context.IsWorldReady || !Context.IsMainPlayer) throw new Exception("Only host saves may be read");
            return this.Store.TryGetValue(key, out string? json) ? JsonSerializer.Deserialize<T>(json) : null;
        }
        public void WriteSaveData<T>(string key, T value)
        {
            if (!Context.IsWorldReady || !Context.IsMainPlayer) throw new Exception("Only host saves may be written");
            this.Store[key] = JsonSerializer.Serialize(value);
            this.Writes++;
        }
        public void Log(string message, LogLevel level) { }
    }
}
namespace StardewValley
{
    internal sealed class Farmer { public HashSet<string> eventsSeen { get; } = new(); }
    internal sealed class Farm { public Dictionary<string, string> modData { get; } = new(); }
    internal static class Game1
    {
        public static object? CurrentEvent { get; set; }
        public static bool eventUp;
        public static bool eventOver;
        public static Farmer MasterPlayer { get; set; } = new();
        public static Farm TestFarm { get; set; } = new();
        public static Farm getFarm()
        {
            if (!StardewModdingAPI.Context.IsWorldReady) throw new Exception("Farm accessed before world ready");
            return TestFarm;
        }
    }
}
