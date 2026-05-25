namespace WebhooksAPI.Data.DTO;

/// <summary>Standard error envelope returned on 4xx responses.</summary>
public record ErrorResponse(
    string Code,
    string Message
);
