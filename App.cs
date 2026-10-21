namespace RAA_Int_Module_02_Skills
{
    internal class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {

            return Result.Succeeded;
        }
    }

}
