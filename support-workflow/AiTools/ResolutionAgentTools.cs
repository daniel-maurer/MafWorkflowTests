using System.ComponentModel;

namespace SupportWorkflow;

/// <summary>
/// Tools for the resolution agent to execute account recovery and communication actions.
/// </summary>
public static class ResolutionAgentTools
{
    /// <summary>
    /// Unlocks a user account that has been locked due to failed login attempts or security issues.
    /// </summary>
    [Description("Unlock a user account that has been locked or suspended.")]
    public static async Task<bool> UnlockAccount(
        [Description("The user account identifier or email address to unlock.")] string accountIdentifier,
        [Description("Optional reason for the unlock request.")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        Logger.LogDebug($"[TOOL CALL] UnlockAccount - Account: {accountIdentifier}, Reason: {reason ?? "No reason provided"}");
        
        // Simulate account unlock operation
        await Task.Delay(100, cancellationToken);
        
        Logger.LogDebug($"[TOOL RESULT] UnlockAccount - Success: Account {accountIdentifier} has been unlocked.");
        return true;
    }

    /// <summary>
    /// Sends a templated email to the user for password reset, billing updates, or subscription management.
    /// </summary>
    [Description("Send a templated email to the user for account recovery or support actions.")]
    public static async Task<bool> SendEmail(
        [Description("The recipient email address.")] string recipientEmail,
        [Description("The type of email template to send.")] CommonEmails emailType,
        [Description("Optional additional context or parameters for the email template.")] Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var paramStr = parameters != null ? string.Join(", ", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "None";
        Logger.LogDebug($"[TOOL CALL] SendEmail - Recipient: {recipientEmail}, Type: {emailType}, Parameters: {paramStr}");
        
        // Simulate email sending operation
        await Task.Delay(150, cancellationToken);
        
        Logger.LogDebug($"[TOOL RESULT] SendEmail - Success: Email of type '{emailType}' sent to {recipientEmail}.");
        return true;
    }

    public enum CommonEmails
    {
        [Description("Email to reset password")]
        ResetPassword = 1,

        [Description("Email to update billing information")]
        UpdateBilling = 2,

        [Description("Email to cancel subscription")]
        CancelSubscription = 3,

        [Description("Email to confirm account unlock")]
        UnlockConfirmation = 4
    }
}