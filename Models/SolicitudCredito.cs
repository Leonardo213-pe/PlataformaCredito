using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaCredito.Models;

public enum EstadoSolicitud { Pendiente, Aprobado, Rechazado }

public class SolicitudCredito
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [MaxLength(500)]
    public string? MotivoRechazo { get; set; }

    public Cliente? Cliente { get; set; }
}
