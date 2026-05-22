using System;
using System.Reflection;
using Microsoft.Agents.AI.Workflows;

class Program
{
    static void Main()
    {
        var type = typeof(StreamsMessageAttribute);
        Console.WriteLine($"Type: {type.FullName}");
        
        var usage = type.GetCustomAttribute<AttributeUsageAttribute>();
        if (usage != null)
        {
            Console.WriteLine($"AttributeUsage: ValidOn={usage.ValidOn}, AllowMultiple={usage.AllowMultiple}, Inherited={usage.Inherited}");
        }
        else
        {
            Console.WriteLine("No AttributeUsageAttribute found.");
        }

        foreach (var constructor in type.GetConstructors())
        {
            Console.WriteLine($"Constructor: {constructor}");
            foreach (var parameter in constructor.GetParameters())
            {
                Console.WriteLine($"  Parameter: {parameter.ParameterType} {parameter.Name}");
            }
        }

        foreach (var property in type.GetProperties())
        {
            Console.WriteLine($"Property: {property.PropertyType} {property.Name}");
        }
    }
}
