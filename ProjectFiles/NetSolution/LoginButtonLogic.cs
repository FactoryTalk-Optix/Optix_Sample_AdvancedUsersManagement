#region Using directives
using System;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NetLogic;
using FTOptix.Core;
using FTOptix.UI;
using FTOptix.EventLogger;
using FTOptix.Store;
using FTOptix.Recipe;
using FTOptix.SQLiteStore;
#endregion

public class LoginButtonLogic : BaseNetLogic
{
    public override void Start()
    {
        ComboBox comboBox = Owner.Owner.Get<ComboBox>("Username");
        if (Project.Current.Authentication.AuthenticationMode == AuthenticationMode.ModelOnly)
        {
            comboBox.Mode = ComboBoxMode.Normal;
        }
        else
        {
            comboBox.Mode = ComboBoxMode.Editable;
            Log.Error("LoginButtonLogic", $"Authentication mode {Project.Current.Authentication.AuthenticationMode} is not supported, only Model works with this project");
            throw new NotImplementedException($"Authentication mode {Project.Current.Authentication.AuthenticationMode} is not supported, only Model works with this project");
        }
    }

    public override void Stop()
    {

    }

    [ExportMethod]
    public void PerformLogin(string username, string password)
    {
        // Get Users alias
        var usersAlias = LogicObject.GetAlias("Users");
        if (usersAlias == null || usersAlias.NodeId == NodeId.Empty)
        {
            Log.Error("LoginButtonLogic", "Missing Users alias");
            return;
        }

        // Get PasswordExpiredDialogType alias
        if (LogicObject.GetAlias("PasswordExpiredDialogType") is not DialogType passwordExpiredDialogType)
        {
            Log.Error("LoginButtonLogic", "Missing PasswordExpiredDialogType alias");
            return;
        }

        Button loginButton = (Button) Owner;
        loginButton.Enabled = false;

        // Get user we are trying to login from the Users alias
        var userToLogin = usersAlias.Get<CustomUserType>(username);
        if (userToLogin == null)
        {
            Log.Error("LoginButtonLogic", $"CustomUserType {username} not found");
            DispatchEventToCore(username, false, "User not found");
            loginButton.Enabled = true;
            return;
        }

        // Get block duration
        long blockDurationInMilliseconds = Project.Current.GetVariable("UsersBlockDuration").Value;
        // Get max failed logins
        int maxFailedLogins = Project.Current.GetVariable("UsersMaxFailedLogins").Value;
        // Get output message label
        var outputMessageLabel = Owner.Owner.GetObject("LoginFormOutputMessage");
        var outputMessageLogic = outputMessageLabel.GetObject("LoginFormOutputMessageLogic");

        try
        {
            // Check if user is disabled
            if (userToLogin.ManuallyDisabled)
            {
                Log.Error("LoginButtonLogic", $"User {username} is disabled");
                outputMessageLogic.ExecuteMethod("SetOutputMessage", new object[] { ChangeUserResultCode.LoginAttemptBlocked });
                DispatchEventToCore(username, false, "User is disabled");
                loginButton.Enabled = true;
                return;
            }

            // Check if the user can be unblocked after the block duration
            if (DateTime.Now >= userToLogin.LastFailedLoginTime.AddMilliseconds(blockDurationInMilliseconds))
            {
                userToLogin.FailedLoginsCounter = 0; // Reset failed logins counter
                userToLogin.LastFailedLoginTime = new DateTime(0); // Set last failed login time
            }

            // Check if the user should be unblocked after the block duration
            if (userToLogin.FailedLoginsCounter >= maxFailedLogins)
            {
                Log.Error("LoginButtonLogic", $"User {username} is blocked, too many failed login attempts, try again after {userToLogin.LastFailedLoginTime.AddMilliseconds(blockDurationInMilliseconds)}");
                userToLogin.AutomaticBlockedDate = DateTime.Now;
                userToLogin.LastFailedLoginTime = DateTime.Now;
                outputMessageLogic.ExecuteMethod("SetOutputMessage", new object[] { ChangeUserResultCode.LoginAttemptBlocked });
                DispatchEventToCore(username, false, "Too many failed logins");
                loginButton.Enabled = true;
                return;
            }

            // Perform the login operation in the session
            var loginResult = Session.Login(username, password);
            if (loginResult.ResultCode == ChangeUserResultCode.PasswordExpired || userToLogin.MustChangePassword) // Check if need to change password
            {
                loginButton.Enabled = true;
                var user = usersAlias.Get<CustomUserType>(username);
                var ownerButton = (Button) Owner;
                ownerButton.OpenDialog(passwordExpiredDialogType, user.NodeId);
                userToLogin.FailedLoginsCounter = 0; // Reset failed logins counter
                if (loginResult.ResultCode == ChangeUserResultCode.PasswordExpired)
                {
                    DispatchEventToCore(username, false, "Password expired");
                }
                if (userToLogin.MustChangePassword)
                {
                    DispatchEventToCore(username, false, "Must change password");
                }
                return;
            }
            else if (loginResult.ResultCode != ChangeUserResultCode.Success)
            {
                outputMessageLogic.ExecuteMethod("SetOutputMessage", new object[] { (int) loginResult.ResultCode });
                userToLogin.FailedLoginsCounter++; // Reset failed logins counter
                userToLogin.LastFailedLoginTime = DateTime.Now; // Set last failed login time
            }
            else
            {
                if (loginResult.ResultCode == ChangeUserResultCode.Success)
                {
                    userToLogin.FailedLoginsCounter = 0; // Reset failed logins counter
                    userToLogin.LastFailedLoginTime = new DateTime(0); // Set last failed login time
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("LoginButtonLogic", e.Message);
        }

        loginButton.Enabled = true;
    }

    private void DispatchEventToCore(string username, bool succesful, string message)
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
            succesful,
            message
        };

        // Trigger the event
        eventsDispatcherLogic.ExecuteMethod("TriggerUserSessionEvent", [inputArgs]);
    }
}
