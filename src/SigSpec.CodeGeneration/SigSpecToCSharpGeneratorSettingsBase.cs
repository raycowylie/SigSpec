using NJsonSchema.CodeGeneration;

namespace SigSpec.CodeGeneration
{
    public class SigSpecToCSharpGeneratorSettingsBase : ClientGeneratorBaseSettings
    {
        public SigSpecToCSharpGeneratorSettingsBase(CodeGeneratorSettingsBase codeGeneratorSettings)
        {
            CodeGeneratorSettings = codeGeneratorSettings;
        }

        protected CodeGeneratorSettingsBase CodeGeneratorSettings { get; }
    }
}
