using System.Text.RegularExpressions;

namespace VetWorkflow;

/// <summary>
/// Guardrail de segurança veterinária:
/// Garante que respostas dos agentes NUNCA contenham diagnósticos médicos, prescrições de dosagens
/// ou afirmações que tranquilicem falsamente tutores diante de sinais potencialmente graves.
/// </summary>
public static class VetSafetyGuardrail
{
    private static readonly string[] ForbiddenPrescriptionTerms =
    {
        "tome ", "dar ", "administre ", "dosagem de", "mg/kg", "gotas de", "comprimido de",
        "paracetamol", "dipirona", "ibuprofeno", "ivermectina", "amoxicilina", "prednisolona"
    };

    private static readonly string[] ForbiddenDiagnosisTerms =
    {
        "o diagnóstico é", "seu pet está com cinomose", "com certeza é parvovirose",
        "não é nada grave", "não se preocupe, vai passar sozinho", "isso é bobagem"
    };

    /// <summary>
    /// Verifica e sanitiza o texto antes de enviar para o tutor.
    /// Se violar regra clínica, substitui por mensagem de segurança com escalonamento para o veterinário.
    /// </summary>
    public static string SanitizeOrEscalate(string text, out bool violated)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            violated = false;
            return text;
        }

        var lower = text.ToLowerInvariant();

        foreach (var term in ForbiddenPrescriptionTerms)
        {
            if (lower.Contains(term))
            {
                violated = true;
                Logger.LogWarning($"[SAFETY GUARDRAIL] Bloqueada tentativa de prescrição/dosagem: '{term}'");
                return "⚠️ Informação protegida: Como assistente administrativo, não tenho autorização para prescrever medicamentos ou indicar dosagens. O Dr(a). Veterinário(a) fornecerá a prescrição correta para o caso do seu pet.";
            }
        }

        foreach (var term in ForbiddenDiagnosisTerms)
        {
            if (lower.Contains(term))
            {
                violated = true;
                Logger.LogWarning($"[SAFETY GUARDRAIL] Bloqueada tentativa de diagnóstico/tranquilização falsa: '{term}'");
                return "⚠️ Informação protegida: O diagnóstico clínico só pode ser realizado pelo médico veterinário através de exame presencial e/ou exames complementares. Estou encaminhando para o profissional.";
            }
        }

        violated = false;
        return text;
    }
}
