namespace BadBort.AzureRm.Foundation.Model;

/// <summary>
/// Yaml object that allows managing the creation of ad applications
/// </summary>  
public class AdConfigFile
{
    public List<AdApplication>? Applications { get; set; }
}

/// <summary>
/// Configures an Azure AD application
/// </summary>
public class AdApplication
{
    public AdApplicationServicePrinciple?  ServicePrincipal { get; set; }
}

public class AdApplicationServicePrinciple
{
    
}