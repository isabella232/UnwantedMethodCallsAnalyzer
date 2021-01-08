# Unwanted method calls analyzer

## Purpose

This analyzer allows specifying methods that should not be called in our source code.

## Installation and Setup

The unwanted method calls analyzer can be added to an existing project via the nuget package.

To configure what methods are not allowed you need to add `unwanted_method_calls.json` as an additional file to the project.

```xml
  <ItemGroup>
    <AdditionalFiles Include="unwanted_method_calls.json" />
  </ItemGroup>
```

The configuration file format is as such:  
**Comments in the config file are not supported**

```json
{
  "UnwantedMethods": [
    {
      "TypeNamespace": "System.Diagnostics.Process",
      "MethodName": "Start",
      "ExcludeCheckingTypes": [
        "MyNamespace.ShouldBeIgnoredClass"
      ]
    }
  ]
}
```
