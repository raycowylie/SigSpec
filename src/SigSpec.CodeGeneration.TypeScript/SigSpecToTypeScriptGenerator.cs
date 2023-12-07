using System.Collections.Generic;
using System.Linq;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.TypeScript;
using SigSpec.CodeGeneration.Models;
using SigSpec.CodeGeneration.TypeScript.Models;
using SigSpec.Core;

namespace SigSpec.CodeGeneration.TypeScript
{
    public class SigSpecToTypeScriptGenerator
    {
        private readonly SigSpecToTypeScriptGeneratorSettings _settings;

        public SigSpecToTypeScriptGenerator(SigSpecToTypeScriptGeneratorSettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<CodeArtifact> GenerateArtifacts(SigSpecDocument document)
        {
            TypeScriptTypeResolver resolver = new TypeScriptTypeResolver(_settings.TypeScriptGeneratorSettings);
            resolver.RegisterSchemaDefinitions(document.Definitions);

            List<CodeArtifact> artifacts = new List<CodeArtifact>();
            foreach (KeyValuePair<string, SigSpecHub> hub in document.Hubs)
            {
                HubModel hubModel = new HubModel(hub.Key, hub.Value, resolver);
                ITemplate template = _settings.TypeScriptGeneratorSettings.TemplateFactory.CreateTemplate("TypeScript", "Hub", hubModel);
                artifacts.Add(new CodeArtifact(hubModel.Name, CodeArtifactType.Class, CodeArtifactLanguage.TypeScript, CodeArtifactCategory.Client, template.Render()));
            }

            if (_settings.GenerateDtoTypes)
            {
                TypeScriptGenerator generator = new TypeScriptGenerator(document, _settings.TypeScriptGeneratorSettings, resolver);
                IEnumerable<CodeArtifact> types = generator.GenerateTypes();

                return artifacts.Concat(types);
            }
            else
            {
                TypeScriptGenerator generator = new TypeScriptGenerator(document, _settings.TypeScriptGeneratorSettings, resolver);
                TypeScriptExtensionCode extensionCode = new TypeScriptExtensionCode(_settings.TypeScriptGeneratorSettings.ExtensionCode, _settings.TypeScriptGeneratorSettings.ExtendedClasses);
                return artifacts.Concat(generator.GenerateTypes(extensionCode));
            }
        }

        public string GenerateFile(SigSpecDocument document)
        {
            IEnumerable<CodeArtifact> artifacts = GenerateArtifacts(document);

            FileModel fileModel = new FileModel(artifacts.Select(a => a.Code));
            ITemplate fileTemplate = _settings.TypeScriptGeneratorSettings.TemplateFactory.CreateTemplate("TypeScript", "File", fileModel);

            return fileTemplate.Render();
        }
    }
}
