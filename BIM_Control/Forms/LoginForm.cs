using BIM_Control.Services;
using BIM.Application.Common.Interfaces;
using BIM.Application.Common.Safety;
using BIM.Application.Common.Validator;
using BIM.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using BIM_Control.Services;
using System.IO;
using System.Drawing;

namespace BIM_Control
{
    public partial class LoginForm : Form
    {
        //di
        private readonly LoginVMValidator _loginValidator;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILoggerService _loggerService;
        private readonly IGeneralService _generalService;
        private readonly ICurrentUserService _currentUserService;

        //variables
        private bool _loading;

        public LoginForm(
            UserManager<AppUser> userManager,
            LoginVMValidator loginValidator,
            ILoggerService loggerService,
            IGeneralService generalService,
            ICurrentUserService currentUserService)
        {
            InitializeComponent();
            TrySetAppIcon();

            _userManager = userManager;
            _loginValidator = loginValidator;
            _loggerService = loggerService;
            _generalService = generalService;
            _currentUserService = currentUserService;

            lb_pcName.Text = _generalService.PCName;
        }

        private void TrySetAppIcon()
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "BIMv2.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
            
        }

        private async void btn_login_Click(object sender, EventArgs e)
        {
            LoginViewModel loginViewModel = new()
            {
                UserName = tb_login.Text,
                Password = tb_password.Text
            };
            try
            {
                _loading = true;
                btn_login.Enabled = false;
                _loggerService.LogInformation($"Вход в систему, компьютер - {_generalService.PCName}");

                var isFormValid = _loginValidator.Validate(loginViewModel);
                if (isFormValid.IsValid)
                {
                    var user = await _userManager.FindByNameAsync(loginViewModel.UserName);
                    if (user is null)
                    {
                        _loggerService.LogWarning($"Пользователь с именем {loginViewModel.UserName}, не существует!");
                        MessageBox.Show("Пользователь не найден, обратитесь к администратору", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else if (!user.IsLive)
                    {
                        _loggerService.LogWarning($"{loginViewModel.UserName} удален, но не существует");
                        MessageBox.Show("Этот пользователь заблокирован", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        var result = await _userManager.CheckPasswordAsync(user, loginViewModel.Password);
                        if (!result)
                        {
                            _loggerService.LogWarning($"{user.UserName} не прошел аутентификацию");
                            MessageBox.Show("Пароль пользователя не совпадает", "Ошибка",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            _loggerService.LogInformation($"{user.UserName} вошел в систему");

                            // Store the current user information
                            _currentUserService.SetCurrentUserName(user.UserName);

                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                    }
                }
                else
                {
                    _loggerService.LogError(isFormValid.Errors.FirstOrDefault()!.ErrorMessage);
                    MessageBox.Show(isFormValid.Errors.FirstOrDefault()!.ErrorMessage, "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                _loading = false;
                btn_login.Enabled = true;
            }
        }
    }
}

