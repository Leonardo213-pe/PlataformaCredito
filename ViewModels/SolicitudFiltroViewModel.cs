using PlataformaCredito.Models;

namespace PlataformaCredito.ViewModels;

public class SolicitudFiltroViewModel
{
    // Filtros
    public EstadoSolicitud? Estado { get; set; }
    public decimal? MontoMin { get; set; }
    public decimal? MontoMax { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    // Errores de validación server-side
    public string? ErrorFiltro { get; set; }

    // Resultados
    public List<SolicitudCredito> Solicitudes { get; set; } = new();
}
