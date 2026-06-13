using System.ComponentModel.DataAnnotations;

namespace PlataformaCredito.ViewModels;

public class CrearSolicitudViewModel
{
    [Required(ErrorMessage = "El monto es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
    [Display(Name = "Monto solicitado")]
    public decimal MontoSolicitado { get; set; }
}
