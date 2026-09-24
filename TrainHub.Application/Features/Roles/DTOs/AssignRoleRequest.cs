using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Roles.DTOs;

public class AssignRoleRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(256)]
    public string RoleName { get; set; } = string.Empty;
}
