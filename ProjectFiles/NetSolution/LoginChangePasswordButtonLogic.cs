#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NetLogic;
using FTOptix.NativeUI;
using FTOptix.HMIProject;
using FTOptix.UI;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.Retentivity;
using Quickenshtein;
using FTOptix.EventLogger;
using FTOptix.Store;
using FTOptix.Recipe;
using FTOptix.SQLiteStore;
#endregion

public class LoginChangePasswordButtonLogic : BaseNetLogic
{
    [ExportMethod]
    public void PerformChangePassword(string oldPassword, string newPassword, string confirmPassword)
    {
        var outputMessageLabel = Owner.Owner.GetObject("ChangePasswordFormOutputMessage");
        var outputMessageLogic = outputMessageLabel.GetObject("LoginChangePasswordFormOutputMessageLogic");

        // Calculate levenshteind distance
        var distance = Levenshtein.GetDistance(oldPassword, newPassword); // Calculated using: https://github.com/Turnerj/Quickenshtein
        int minDistance = Project.Current.GetVariable("MinimumLevenshteinDistance").Value; // Get the minimum minimum levenshtein distance from the project root
        if (distance < minDistance)
        {
            Log.Error("PerformChangePassword", "Levenshtein minimum distance is not met"); // Log error to the Runtime output
            outputMessageLogic.ExecuteMethod("SetOutputMessage", new object[] { (int) ChangePasswordResultCode.PasswordTooShort }); // Set output message to "PasswordTooShort"
            DispatchEventToCore(Session.User.BrowseName, false, "Levenshtein distance not met"); // Dispatch event to the core of FT Optix
            return;
        }

        // Check if password matches the desired regex
        var passwordRegex = Project.Current.GetVariable("PasswordRegex").Value; // Get the password regex from the project root
        if (!System.Text.RegularExpressions.Regex.IsMatch(newPassword, passwordRegex)) // Check if the password matches the regex
        {
            Log.Error("PerformChangePassword", "Password does not match the desired regex"); // Log error to the Runtime output
            outputMessageLogic.ExecuteMethod("SetOutputMessage", new object[] { (int) ChangePasswordResultCode.PasswordTooShort }); // Set output message to "PasswordTooShort"
            DispatchEventToCore(Session.User.BrowseName, false, "Password does not match the desired regex"); // Dispatch event to the core of FT Optix
            return;
        }

        // Perform password change on Optix core
        if (newPassword != confirmPassword) // Check if passwords are matching
        {
            outputMessageLogic.ExecuteMethod("SetOutputMessage", new object[] { 7 }); // Set output message to "Passwords do not match"
        }
        else
        {
            var username = Session.User.BrowseName; // Get the username of the current session
            try
            {
                var userWithExpiredPassword = Owner.GetAlias("UserWithExpiredPassword");
                if (userWithExpiredPassword != null)
                    username = userWithExpiredPassword.BrowseName;
            }
            catch
            {
            }

            var result = Session.ChangePassword(username, newPassword, oldPassword); // Perform the password change to the core
            if (result.ResultCode == ChangePasswordResultCode.Success) // Check if the password change was successful
            {
                var parentDialog = Owner.Owner?.Owner?.Owner as Dialog;
                if (result.Success)
                {
                    Project.Current.Get("Security/Users").Get<CustomUserType>(username).MustChangePassword = false; // Set the MustChangePassword to false
                    DispatchEventToCore(username, true, "Password changed successfully"); // Dispatch event to the core of FT Optix
                    parentDialog?.Close(); // Close the dialog
                }
            }
            outputMessageLogic.ExecuteMethod("SetOutputMessage", new object[] { (int)result.ResultCode }); // Set the output message to the result code
        }
    }

    private void DispatchEventToCore(string username, bool successful, string message)
    {
        // Get the EventDispatcher NetLogic
        var eventsDispatcherLogic = Project.Current.GetObject("NetLogic/EventsDispatcher");
        if (eventsDispatcherLogic == null)
        {
            Log.Error("LoginButtonLogic", "EventsDispatcher NetLogic not found");
            return;
        }

        // Create the input arguments for the event
        var inputArgs = new object[]
        {
            "LoginForm",
            Owner.NodeId,
            username,
            successful,
            message
        };

        // Trigger the event
        eventsDispatcherLogic.ExecuteMethod("TriggerUserSessionEvent", [inputArgs]);
    }
}
