using System.Reflection;

namespace FfxivVR;

public class ConfigManager(Configuration configuration, Logger logger)
{
    public void SetConfig(string name, string value)
    {
        if (configuration.GetType().GetField(name) is not FieldInfo field)
        {
            logger.Error($"No configuration with name \"{name}\"");
            return;
        }
        var isNull = value == "null";
        if (field.FieldType == typeof(float))
        {
            if (float.TryParse(value, out float floatValue))
            {
                field.SetValue(configuration, floatValue);
                logger.Info($"Setting {field.Name} to {floatValue}");
            }
            else
            {
                logger.Error("Error, expected a number");
            }
        }
        else if (field.FieldType == typeof(bool))
        {
            if (bool.TryParse(value, out bool boolValue))
            {
                field.SetValue(configuration, boolValue);
                logger.Info($"Setting {field.Name} to {boolValue}");
            }
            else
            {
                logger.Error("Error, expected true or false");
            }
        }
        else if (field.FieldType == typeof(int?))
        {
            if (isNull)
            {
                field.SetValue(configuration, null);
                logger.Info($"Setting {field.Name} to {null}");
            }
            else if (int.TryParse(value, out int intValue))
            {
                field.SetValue(configuration, intValue);
                logger.Info($"Setting {field.Name} to {intValue}");
            }
            else
            {
                logger.Error("Error, expected an integer");
            }
        }
        else
        {
            logger.Error($"Unsupported field type ${field.FieldType}");
        }
        configuration.Save();
    }
}