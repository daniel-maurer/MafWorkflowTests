using System.ComponentModel;
using System.Text.Json;

namespace VetWorkflow;

public static class VetPatientFileTools
{
    private static readonly Dictionary<string, PatientData> _patientStore = new(StringComparer.OrdinalIgnoreCase);

    [Description("Cria uma nova ficha de paciente ou atualiza uma existente com os dados coletados.")]
    public static async Task<string> CreateOrUpdatePatientFile(
        [Description("Nome do animal")] string petName,
        [Description("Nome do tutor")] string tutorName,
        [Description("Telefone do tutor")] string tutorPhone,
        [Description("JSON com os dados completos do paciente")] string patientDataJson,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Criando/atualizando ficha para {petName} (Tutor: {tutorName})");
        await Task.Delay(40, cancellationToken);

        string patientId = $"PAT-{Math.Abs((petName + tutorName).GetHashCode()):X6}";
        PatientData patient;

        try
        {
            patient = JsonSerializer.Deserialize<PatientData>(patientDataJson) ?? new PatientData
            {
                PetName = petName,
                Species = "cão",
                TutorName = tutorName,
                TutorPhone = tutorPhone
            };
        }
        catch
        {
            patient = new PatientData
            {
                PetName = petName,
                Species = "cão",
                TutorName = tutorName,
                TutorPhone = tutorPhone
            };
        }

        patient.PatientId = patientId;
        _patientStore[patientId] = patient;

        return JsonSerializer.Serialize(new
        {
            success = true,
            patientId,
            message = $"Ficha do paciente {petName} salva com sucesso.",
            totalAppointmentsInHistory = 2
        });
    }

    [Description("Busca histórico de atendimentos anteriores do paciente pelo nome do animal e identificador do tutor.")]
    public static async Task<string> SearchPatientHistory(
        [Description("Nome do animal")] string petName,
        [Description("Nome ou telefone do tutor")] string tutorIdentifier,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInfo($"[TOOL] Buscando histórico anterior para {petName}...");
        await Task.Delay(40, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            found = true,
            petName,
            lastVisit = DateTime.UtcNow.AddMonths(-6).ToString("yyyy-MM-dd"),
            pastProcedures = new[] { "Consulta de rotina", "Vacina V10", "Vermifugação" },
            knownAllergies = "Nenhuma alergia relatada"
        });
    }
}
