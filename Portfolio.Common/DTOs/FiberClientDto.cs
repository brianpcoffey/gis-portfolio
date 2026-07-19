namespace Portfolio.Common.DTOs;

/// <summary>
/// A fiber-network client (customer) including contact details and location.
/// </summary>
public class FiberClientDto
{
    /// <summary>Unique identifier of the client.</summary>
    public int Id { get; set; }

    /// <summary>Client company or account name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Name of the primary contact person at the client.</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>Contact email address for the client.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Contact phone number for the client.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>City where the client is located.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>State where the client is located.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>WGS84 latitude of the client's location.</summary>
    public double Latitude { get; set; }

    /// <summary>WGS84 longitude of the client's location.</summary>
    public double Longitude { get; set; }

    /// <summary>UTC timestamp when the client record was created.</summary>
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Request body for creating a new fiber-network client.
/// </summary>
public class CreateFiberClientDto
{
    /// <summary>Client company or account name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Name of the primary contact person at the client.</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>Contact email address for the client.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Contact phone number for the client.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>City where the client is located.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>State where the client is located.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>WGS84 latitude of the client's location.</summary>
    public double Latitude { get; set; }

    /// <summary>WGS84 longitude of the client's location.</summary>
    public double Longitude { get; set; }
}
