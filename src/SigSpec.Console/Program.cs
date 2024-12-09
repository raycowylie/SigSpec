using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SigSpec.CodeGeneration.CSharp;
using SigSpec.CodeGeneration.TypeScript;
using SigSpec.Core;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Diagnostics.Tracing;

namespace SigSpec
{
    static class Program
    {
        private static string? csharpFile = null;
        private static string? jsonFile = null;
        private static string? typescriptFile = null;
        private static string? targetAssemblyName = null;
        private static List<string> removeProperties = new List<string>();
        private static List<string> removeClass = new List<string>();
        private static Dictionary<string,string[]> keepProperties = new ();
        private static Dictionary<string,string[]> removeTypes = new ();

        private static string? targetFile = null;
        
        
        private static string? assemblyNamespace = null;
        
            
        static void Main(string[] args)
        {
            ProcessArgs(args);
            
            
            //string dllname = Path.GetFileNameWithoutExtension(targetAssemblyName);
            string dllname = targetAssemblyName;
            string currentDir = Directory.GetCurrentDirectory();

            Directory.SetCurrentDirectory(Path.GetDirectoryName(targetFile)!);

            string zipAssemblies = Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(), "SigSpecSignalRAssemblies");
            UnloadSigSpecAssembly(zipAssemblies, true);

            LoadAssemblies(zipAssemblies);
            
            Assembly targetAss = AppDomain.CurrentDomain.GetAssemblies().First(a => a.FullName.StartsWith(dllname));
            
                        
            
            var settings = new SigSpecGeneratorSettings();
            settings.UseXmlDocumentation = true;
            
            var generator = new SigSpecGenerator(settings);
            
            
            Type[] types = targetAss.GetTypes();
            
            Type[] hubs = types.Where(t => t.BaseType?.FullName?.Contains("Microsoft.AspNetCore.SignalR.Hub") ?? false).ToArray();

            var document = generator.GenerateForHubsAsync(new Dictionary<string, Type>(hubs.Select(t => new KeyValuePair<string, Type>(t.Name, t))));

            //On retire les noms de classe qui contiennent des ` pour éviter les erreurs de génération de code.
            //Normalement ce nom provient d'une classe générique. Juste retire ce qui suit le ` devrait suffire.
            List<KeyValuePair<string,SigSpecHub>> invalideHubs = new ();
            foreach (var hub in document.Hubs)
            {
                if (hub.Value.Name.Contains('`'))
                {
                    hub.Value.Name = hub.Value.Name[0..hub.Value.Name.IndexOf('`')];
                    invalideHubs.Add(hub);
                }
            }
            //Comme on se fit au nom de la clé, il faut retirer les anciennes clés et ajouter les nouvelles.
            foreach (var hub in invalideHubs)
            {
                document.Hubs.Remove(hub.Key);
                document.Hubs.Add(hub.Value.Name, hub.Value);
            }


            Directory.SetCurrentDirectory(currentDir);

            foreach (string item in removeClass)
            {
                if (document.Definitions.ContainsKey(item))
                    document.Definitions.Remove(item);
            }

            foreach (string item in removeProperties)
            {
                if(document.Definitions.ContainsKey(item))
                    document.Definitions.Remove(item);
            }

            foreach (var item in keepProperties)
            {
                if (document.Definitions.ContainsKey(item.Key))
                {
                    var schema = document.Definitions[item.Key];
                    foreach (var key in schema.Properties.Keys)
                    {
                        if (!item.Value.Contains(key))
                            schema.Properties.Remove(key);
                    }
                }
            }

            foreach (var typeToRemove in removeTypes)
            {
                foreach(var item in typeToRemove.Value)
                {
                    if (document.Definitions.ContainsKey(item))
                    {
                        foreach (var key in document.Definitions[item].Properties.Keys)
                        {
                            if ((document.Definitions[item].Properties[key].Format?.ToLower() ?? string.Empty) == typeToRemove.Key.ToLower())
                            {
                                document.Definitions[item].Properties.Remove(key);
                            }
                        }
                    }
                }
            }

            ////On va chercher RSimplified et on retire les champs qui ne sont pas pertinent.
            //var rSimplified = document.Definitions["RSimplified"];
            //foreach (var key in rSimplified.Properties.Keys)
            //{
            //    if (key != "stringID" && key != "properties" && key != "objectValue")
            //        rSimplified.Properties.Remove(key);
            //}

            //RemoveLong(document.Definitions["DiagSensor"]);
            //RemoveLong(document.Definitions["DiagItem"]);

            var json = document.ToJson();
            if(jsonFile is null)
                Console.WriteLine(json);
            else
                File.WriteAllText(jsonFile, json, Encoding.UTF8);

            if (typescriptFile is not null)
            {
                var tsCodeGeneratorSettings = new SigSpecToTypeScriptGeneratorSettings();
                var tsCodeGenerator = new SigSpecToTypeScriptGenerator(tsCodeGeneratorSettings);
                var file = tsCodeGenerator.GenerateFile(document);

                File.WriteAllText(typescriptFile, file);
            }

            if (csharpFile is not null)
            {
                var codeGeneratorSettings = new SigSpecToCSharpGeneratorSettings();
                
                if (assemblyNamespace is not null)
                    codeGeneratorSettings.CSharpGeneratorSettings.Namespace = assemblyNamespace;
                var codeGenerator = new SigSpecToCSharpGenerator(codeGeneratorSettings);
                var file = codeGenerator.GenerateClients(document);

                using StreamWriter sw = new StreamWriter(csharpFile, Encoding.UTF8, new FileStreamOptions() { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.ReadWrite });
                string[] lines = file.Split("\n");
                foreach (string line in lines)
                {
                    if (line.Contains("Newtonsoft"))
                        continue;
                    sw.WriteLine(line);
                }
                sw.Close();
            }
        }

        public static void ProcessArgs(string[] args)
        {
            if (args.Length == 0)
            {
                DisplayHelp();
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    if (args[i].StartsWith("--help") && args.Length > i + 1)
                    {
                        DisplayHelp();
                        return;
                    }
                    else if (args[i].StartsWith("--json") && args.Length > i + 1)
                    {
                        i++;
                        jsonFile = args[i];
                    }
                    else if (args[i].StartsWith("--csharp") && args.Length > i + 1)
                    {
                        i++;
                        csharpFile = args[i];
                    }
                    else if (args[i].StartsWith("--typescript"))
                    {
                        i++;
                        typescriptFile = args[i];
                    }
                    else if (args[i].StartsWith("--assembly"))
                    {
                        i++;
                        targetAssemblyName = args[i];
                    }
                    else if (args[i].StartsWith("--namespace"))
                    {
                        i++;
                        assemblyNamespace = args[i];
                    }
                    else if (args[i].StartsWith("--remove-properties"))
                    {
                        i++;
                        removeProperties.Add(args[i]);
                    }
                    else if (args[i].StartsWith("--remove-class"))
                    {
                        i++;
                        removeClass.AddRange(args[i].Split(","));
                    }
                    else if (args[i].StartsWith("--keep-properties"))
                    {
                        i++;
                        string[] parts = args[i].Split("[");
                        string[] properties = parts[1].TrimEnd(']').Split(',');
                        keepProperties.Add(parts[0], properties);
                    }
                    else if (args[i].StartsWith("--remove-type"))
                    {
                        i++;
                        string[] type = args[i].Split("[");
                        string[] items = type[1].TrimEnd(']').Split(',');
                        removeTypes.Add(type[0], items);
                    }

                }
                else if (args[i].StartsWith("-"))
                {
                    if (args[i].Contains('h') && args.Length > i + 1)
                    {
                        DisplayHelp();
                        return;
                    }
                    else if (args[i].EndsWith('j') && args.Length > i + 1)
                    {
                        i++;
                        jsonFile = args[i];
                    }
                    else if (args[i].EndsWith("c") && args.Length > i + 1)
                    {
                        i++;
                        csharpFile = args[i];
                    }
                    else if (args[i].EndsWith("t") && args.Length > i + 1)
                    {
                        i++;
                        typescriptFile = args[i];
                    }
                    else if (args[i].EndsWith("a") && args.Length > i + 1)
                    {
                        i++;
                        targetAssemblyName = args[i];
                    }
                    else if (args[i].EndsWith("r"))
                    {
                        i++;
                        removeProperties.Add(args[i]);
                    }
                }
                else
                {
                    targetFile = String.Join(" ", args.TakeLast(args.Length - i));
                }
            }
            if(targetFile is null)
                DisplayHelp(); 
        }

        private static void DisplayHelp()
        {
            Console.Error.WriteLine("Usage: sigspec [options] target" + Environment.NewLine +
                                    "SigSpec is a tool building specifications documents for SignalR Core hubs." + Environment.NewLine +
                                    "Options:" + Environment.NewLine +
                                    "  -h, --help\t\t\tDisplay this help message." + Environment.NewLine +
                                    "  -a, --assembly <file>\t\tSpecify the target assembly containing the SignalR Core hubs." + Environment.NewLine +
                                    "  -j, --json <file>\t\tSpecify the output file for the SigSpec JSON document." + Environment.NewLine +
                                    "  -c, --csharp <file>\t\tSpecify the output file for the C# clients." + Environment.NewLine +
                                    "  -t, --typescript <file>\tSpecify the output file for the TypeScript clients." + Environment.NewLine +
                                    "  -r, --remove-properties\t\tProperties to remove." + Environment.NewLine +
                                    "  --keep-properties\t\tProperties to keep in a specific class. Ex: RaycoWylie.Data.Types.RSimplified[objectValue,stringID,properties]" + Environment.NewLine +
                                    "  --remove-type\t\tTypes to remove from class. Ex: UInt64[DiagSensor,DiagItem]" + Environment.NewLine +
                                    "  --remove-class\t\tClass or type to remove from output. Ex: MethodInfo,Delegate" + Environment.NewLine +
                                    "  --namespace <namespace>\tSpecify the namespace for the C# clients." + Environment.NewLine +
                                    "" + Environment.NewLine +
                                    "Arguments:" + Environment.NewLine +
                                    "  target\t\t\t\tSpecify the library files to process. If no assembly option is specified, " + Environment.NewLine +
                                    "        \t\t\t\tthe target name, without extension, is used." + Environment.NewLine +
                                    "" + Environment.NewLine +
                                    "Output:" + Environment.NewLine +
                                    "  If no output option is specified(json,csharp or typescript), the json output is written to the console." + Environment.NewLine +
                                    "" + Environment.NewLine +
                                    "Examples:" + Environment.NewLine +
                                    "  sigspec -j sigspec.json -c sigspec.cs -t sigspec.ts controller.dll" + Environment.NewLine +
                                    "      Analyse controller.dll, using \"controller\" as the assembly name to search for SignalR Hubs." + Environment.NewLine +
                                    "      Write the SigSpec JSON document to sigspec.json, the C# clients to sigspec.cs and the TypeScript clients to sigspec.ts." + Environment.NewLine +
                                    "" + Environment.NewLine +
                                    "  sigspec -a RaycoWylie.Server.FlatTopTC controller.dll" + Environment.NewLine +   
                                    "      Analyse controller.dll, using \"RaycoWylie.Server.FlatTopTC\" as the assembly name to search for SignalR Hubs." + Environment.NewLine +
                                    "      Write the SigSpec JSON document to the console.");

        }

        private static void LoadAssemblies(string dirZipAssemblies)
        {
            string dllDir = Path.GetDirectoryName(targetFile)!;
            string[] dlls = Directory.GetFiles(dllDir, "*.dll");

            foreach (string dll in dlls)
                AppDomain.CurrentDomain.Load(File.ReadAllBytes(dll));

            //foreach (string dll in Directory.GetFiles(Path.Join(dllDir, "win-x64"), "*.dll"))
            //{
            //    try
            //    {
            //        AppDomain.CurrentDomain.Load(File.ReadAllBytes(dll));
            //    }
            //    catch (BadImageFormatException e)
            //    {
            //        Console.Error.WriteLine("Not loaded : " + dll);
            //    }
            //    catch (Exception e)
            //    {
            //        Console.Error.WriteLine("Not loaded : " + dll);
            //    }
            //}

            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                Console.Write(eventArgs.Name);
                var asss = AppDomain.CurrentDomain.GetAssemblies();

                Assembly? ass = asss.FirstOrDefault(a => a.ToString() == eventArgs.Name);
                if (ass is not null)
                {
                    Console.WriteLine(ass.GetName().ToString() + "\t" + ass.Location);
                    Console.WriteLine("\t\tPre-Loaded");
                    return ass;
                }

                string assemblyPath = Path.Combine(dllDir, new AssemblyName(eventArgs.Name).Name + ".dll");
                if (File.Exists(assemblyPath))
                {
                    ass = Assembly.LoadFile(assemblyPath);
                    Console.WriteLine(ass.GetName().ToString() + "\t" + ass.Location);
                    Console.WriteLine("\t\tLOADED");
                    return ass;
                }


                assemblyPath = Path.Combine(dirZipAssemblies, new AssemblyName(eventArgs.Name).Name + ".dll");
                if (File.Exists(assemblyPath))
                {
                    ass = Assembly.LoadFile(assemblyPath);
                    Console.WriteLine(ass.GetName().ToString() + "\t" + ass.Location);
                    Console.WriteLine("\t\tLOADED");
                    return ass;
                }

                string? pathVar = Environment.GetEnvironmentVariable("PATH");
                string[] paths = pathVar?.Split(';') ?? new string[0];
                foreach (string path in paths)
                {
                    assemblyPath = Path.Join(path, new AssemblyName(eventArgs.Name).Name + ".dll");
                    if (File.Exists(assemblyPath))
                    {
                        ass = Assembly.LoadFile(assemblyPath);
                        Console.WriteLine(ass.GetName().ToString() + "\t" + ass.Location);
                        Console.WriteLine("\t\tLOADED");
                        return ass;
                    }
                }


                ////string tmpDir = string.Join("", RandomNumberGenerator.GetBytes(6).Select(b => b.ToString("x2")));
                //string tmpDir = "sigspecLibs";
                //if(Directory.Exists(tmpDir))
                //    Directory.Delete(tmpDir, true);

                //Directory.CreateDirectory(tmpDir);
                //File.WriteAllBytes(Path.Combine(tmpDir, "dlls.zip"), SigSpec.Resources.res);
                //using (FileStream fs = File.OpenRead(Path.Combine(tmpDir, "dlls.zip")))
                //{
                //    ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read);
                //    zip.ExtractToDirectory(tmpDir);
                //}

                //Directory.Delete(tmpDir, true);

                
                Console.WriteLine("\t\tNOT FOUND //!\\\\.");
                return null;
            };
            
        }

        private static void UnloadSigSpecAssembly(string tmpDir, bool delete)
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);

            Directory.CreateDirectory(tmpDir);
            File.WriteAllBytes(Path.Combine(tmpDir, "dlls.zip"), SigSpec.Resources.res);
            using (FileStream fs = File.OpenRead(Path.Combine(tmpDir, "dlls.zip")))
            {
                ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read);
                zip.ExtractToDirectory(tmpDir);
            }

            if (delete)
            {
                AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
                {
                    try
                    {
                        Directory.Delete(tmpDir, true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Error deleting temporary directory : " + ex.Message);
                    }
                };
            }
            
        }

        private static void RemoveLong(NJsonSchema.JsonSchema schema)
        {
            foreach(var key in schema.Properties.Keys)
            {
                if(schema.Properties[key].Format == "int64")
                {
                    schema.Properties.Remove(key);
                }
            }
        }
    }
}
