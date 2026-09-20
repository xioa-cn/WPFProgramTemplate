
using MachineApplication.Entrance.Resources;

namespace MachineApplication.Entrance.ViewModels
{
    public class ViewModelLocator
    {
        public static EntranceLang EntranceLang { get; } = new EntranceLang();
        
        internal ViewModelLocator()
        {
            
        }
    }
}
