using System.Collections.Generic;
using System.Linq;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.CSharp;
using SigSpec.CodeGeneration.CSharp.Models;
using SigSpec.CodeGeneration.Models;
using SigSpec.Core;

namespace SigSpec.CodeGeneration.CSharp
{
    public class SigSpecToCSharpGenerator
    {
        private readonly SigSpecToCSharpGeneratorSettings _settings;

        public SigSpecToCSharpGenerator(SigSpecToCSharpGeneratorSettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<CodeArtifact> GenerateArtifacts(SigSpecDocument document)
        {
            CSharpTypeResolver resolver = new CSharpTypeResolver(_settings.CSharpGeneratorSettings);
            resolver.RegisterSchemaDefinitions(document.Definitions);

            List<CodeArtifact> artifacts = new List<CodeArtifact>();
            foreach (KeyValuePair<string, SigSpecHub> hub in document.Hubs)
            {
                HubModel hubModel = new HubModel(hub.Key, hub.Value, resolver);
                ITemplate template = _settings.CSharpGeneratorSettings.TemplateFactory.CreateTemplate("CSharp", "Hub", hubModel);
                artifacts.Add(new CodeArtifact(hubModel.Name, CodeArtifactType.Class, CodeArtifactLanguage.CSharp, CodeArtifactCategory.Client, template.Render()));
            }

            if (_settings.GenerateDtoTypes)
            {
                CSharpGenerator generator = new CSharpGenerator(document, _settings.CSharpGeneratorSettings, resolver);
                IEnumerable<CodeArtifact> types = generator.GenerateTypes();
                return artifacts.Concat(types);
            }
            else
            {
                CSharpGenerator generator = new CSharpGenerator(document, _settings.CSharpGeneratorSettings, resolver);
                return artifacts.Concat(generator.GenerateTypes());
            }
        }

        public string GenerateClients(SigSpecDocument document)
        {
            IEnumerable<CodeArtifact> artifacts = GenerateArtifacts(document);

            FileModel fileModel = new FileModel(artifacts.Select(a => a.Code), _settings.CSharpGeneratorSettings.Namespace);
            ITemplate fileTemplate = _settings.CSharpGeneratorSettings.TemplateFactory.CreateTemplate("CSharp", "File", fileModel);

            return fileTemplate.Render();
        }
    }
}
