namespace BIM_Control.Services
{
    public static class CurrentUserStore
    {
        public static string CurrentUserName { get; set; } = string.Empty;
        public static bool IsLoggedIn { get; set; } = false;
        
        public static void SetCurrentUser(string userName)
        {
            CurrentUserName = userName;
            IsLoggedIn = !string.IsNullOrEmpty(userName);
        }
        
        public static void ClearCurrentUser()
        {
            CurrentUserName = string.Empty;
            IsLoggedIn = false;
        }
    }
}