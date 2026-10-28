using CommunityToolkit.Mvvm.ComponentModel;
using Machine.ModuleLoad.ModuleConfig;

namespace Machine.ModuleLoad.Mvvm;

public class ViewModelBase : ObservableObject {
    protected IServiceProvider? Provider { get; }

    public ViewModelBase() {
    }

    public ViewModelBase(string providerName) {
        if (string.IsNullOrEmpty(providerName)) {
            throw new ArgumentNullException(nameof(providerName));
        }

        this.Provider = providerName == "super"
            ? MainProvider.ServiceProvider
            : ModuleProvider.GetModuleProvider(providerName);
    }
}