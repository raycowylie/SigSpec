# SigSpec for SignalR Core
Forked from https://github.com/RicoSuter/SigSpec

Modified to be used as a CLI.

## How to use
 - Build SigSpec/src/SigSpec.Console project
 - Publish
 - Add exe to the PATH

```
Usage: sigspec [options] target
SigSpec is a tool building specifications documents for SignalR Core hubs.
Options:
  -h, --help                    Display this help message.
  -a, --assembly <file>         Specify the target assembly containing the SignalR Core hubs.
  -j, --json <file>             Specify the output file for the SigSpec JSON document.
  -c, --csharp <file>           Specify the output file for the C# clients.
  -t, --typescript <file>       Specify the output file for the TypeScript clients.
  -r, --remove-properties               Properties to remove.
  --keep-properties             Properties to keep in a specific class. Ex: RaycoWylie.Data.Types.RSimplified[objectValue,stringID,properties]
  --remove-type         Types to remove from class. Ex: UInt64[DiagSensor,DiagItem]
  --namespace <namespace>       Specify the namespace for the C# clients.

Arguments:
  target                                Specify the library files to process. If no assembly option is specified,
                                        the target name, without extension, is used.

Output:
  If no output option is specified(json,csharp or typescript), the json output is written to the console.

Examples:
  sigspec -j sigspec.json -c sigspec.cs -t sigspec.ts controller.dll
      Analyse controller.dll, using "controller" as the assembly name to search for SignalR Hubs.
      Write the SigSpec JSON document to sigspec.json, the C# clients to sigspec.cs and the TypeScript clients to sigspec.ts.

  sigspec -a RaycoWylie.Server.FlatTopTC controller.dll
  ```

## Use with Rayco projects
Most project are already setup to compile the interfaces needed.
Usually, adding something like this in the project do the trick:
```xml

    <Target Name="GenerateSpecs" AfterTargets="Build">
        <MakeDir ContinueOnError="WarnAndContinue" Directories="$(ProjectDir)protocol" />

        <Exec 
            ContinueOnError="WarnAndContinue" 
            Command="sigspec -a RaycoWylie.Server ^
                             -j $(ProjectDir)protocol/ServerTestClient.json ^
                             -c $(ProjectDir)protocol/ServerTestClient.cs ^
                             -t $(ProjectDir)protocol/ServerTestClient.ts ^
                             --namespace RaycoWylie.Server.Test.Client ^
                             --keep-properties RSimplified[objectValue,stringID,properties] ^
                             --remove-type int64[DiagSensor,DiagItem] ^
                             $(ProjectDir)$(OutDir)/RaycoWylie.Server.dll" />

        <Copy ContinueOnError="WarnAndContinue" 
              
              SourceFiles="$(ProjectDir)protocol/ServerTestClient.cs" 
              DestinationFiles="$(ProjectDir)/../RaycoWylie.Server.Test/ServerTestClient.cs" />

    </Target>
```
