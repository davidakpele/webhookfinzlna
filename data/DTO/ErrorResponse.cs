namespace WebhooksAPI.Data.DTO;

public record ErrorResponse(
    string Code,
    string Message
);
