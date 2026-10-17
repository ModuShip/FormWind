using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind;

namespace FormWindApp;

/// <summary>Sign-in screen: a centered card with validated fields.</summary>
internal sealed class SignInForm : Form
{
    private readonly TailwindProvider _tw = new TailwindProvider();
    private readonly TwTextBox _email = new TwTextBox { PlaceholderText = "you@example.com", Size = new Size(320, 38) };
    private readonly TwTextBox _password = new TwTextBox { UseSystemPasswordChar = true, Size = new Size(320, 38) };

    public SignInForm()
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = "Sign in";
        ClientSize = new Size(440, 460);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var page = new TwPanel { Dock = DockStyle.Fill };
        var card = new TwPanel { Size = new Size(380, 400) };
        var title = new Label { Text = "Welcome back", AutoSize = true };
        var subtitle = new Label { Text = "Sign in to continue to FormWindApp.", AutoSize = true };
        var emailField = new TwField { LabelText = "Email", Required = true, Validator = TwValidators.Email };
        emailField.Controls.Add(_email);
        var passwordField = new TwField { LabelText = "Password", Required = true, Validator = TwValidators.MinLength(6), HintText = "At least 6 characters." };
        passwordField.Controls.Add(_password);
        var remember = new TwCheckBox { Text = "Remember me", AutoSize = true, Checked = true };
        var signIn = new TwButton { Text = "Sign in", Size = new Size(320, 40) };
        signIn.Click += OnSignIn;
        AcceptButton = signIn;

        card.Controls.AddRange(new Control[] { title, subtitle, emailField, passwordField, remember, signIn });
        page.Controls.Add(card);
        Controls.Add(page);

        _tw.SetClasses(this, "page");
        _tw.SetClasses(page, "flex items-center justify-center page");
        _tw.SetClasses(card, "panel flex flex-col gap-3 p-8 shadow-lg");
        _tw.SetClasses(title, "title");
        _tw.SetClasses(subtitle, "muted mb-2");
        _tw.SetClasses(emailField, "field");
        _tw.SetClasses(passwordField, "field");
        _tw.SetClasses(_email, "input");
        _tw.SetClasses(_password, "input");
        _tw.SetClasses(remember, "toggle text-sm");
        _tw.SetClasses(signIn, "btn-primary mt-2");
        _tw.DarkMode = AppState.DarkMode;
        ResumeLayout();
    }

    private void OnSignIn(object? sender, EventArgs e)
    {
        // Shows the errors under the fields and focuses the first invalid one.
        if (!_tw.Validate(this)) return;

        // Replace with your own authentication.
        AppState.UserName = _email.Text.Trim();
        DialogResult = DialogResult.OK;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tw.Dispose();
        base.Dispose(disposing);
    }
}
