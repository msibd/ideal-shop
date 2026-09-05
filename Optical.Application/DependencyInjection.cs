using Microsoft.Extensions.DependencyInjection;
using Optical.Application.Features.Account;
using Optical.Application.Features.Brands;
using Optical.Application.Features.Categories;
using Optical.Application.Features.Customers;
using Optical.Application.Features.Dashboard;
using Optical.Application.Features.Inventory;
using Optical.Application.Features.Permissions;
using Optical.Application.Features.POS;
using Optical.Application.Features.Products;
using Optical.Application.Features.Purchases;
using Optical.Application.Features.Reports;
using Optical.Application.Features.Exchanges;
using Optical.Application.Features.Sales;
using Optical.Application.Features.Settings;
using Optical.Application.Features.Suppliers;
using Optical.Application.Features.Users;

namespace Optical.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<GetProfileHandler>();
        services.AddScoped<UpdateProfileHandler>();

        services.AddScoped<ManageUsersHandler>();

        // Scoped: the permission matrix is read several times while a page renders.
        services.AddScoped<RolePermissionStore>();

        services.AddScoped<GetDashboardSummaryHandler>();

        services.AddScoped<GetCategoriesHandler>();
        services.AddScoped<GetCategoryByIdHandler>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<UpdateCategoryHandler>();

        services.AddScoped<GetBrandsHandler>();
        services.AddScoped<GetBrandByIdHandler>();
        services.AddScoped<CreateBrandHandler>();
        services.AddScoped<UpdateBrandHandler>();

        services.AddScoped<GetProductsHandler>();
        services.AddScoped<GetProductByIdHandler>();
        services.AddScoped<GetProductFormOptionsHandler>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();

        services.AddScoped<GetCustomersHandler>();
        services.AddScoped<GetCustomerByIdHandler>();
        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<UpdateCustomerHandler>();

        services.AddScoped<GetSuppliersHandler>();
        services.AddScoped<GetSupplierByIdHandler>();
        services.AddScoped<CreateSupplierHandler>();
        services.AddScoped<UpdateSupplierHandler>();

        services.AddScoped<GetPurchasesHandler>();
        services.AddScoped<GetPurchaseByIdHandler>();
        services.AddScoped<GetPurchaseFormOptionsHandler>();
        services.AddScoped<CreatePurchaseHandler>();

        services.AddScoped<SearchPosProductsHandler>();
        services.AddScoped<SearchPosCustomersHandler>();
        services.AddScoped<GetLastSaleHandler>();
        services.AddScoped<CheckoutHandler>();

        services.AddScoped<GetSalesHandler>();
        services.AddScoped<GetSaleByIdHandler>();

        services.AddScoped<GetInventoryHandler>();
        services.AddScoped<GetProductStockHandler>();
        services.AddScoped<AdjustStockHandler>();

        services.AddScoped<GetExchangesHandler>();
        services.AddScoped<GetExchangeByIdHandler>();
        services.AddScoped<GetExchangeableSaleHandler>();
        services.AddScoped<CreateExchangeHandler>();

        services.AddScoped<GetSalesReportHandler>();
        services.AddScoped<GetInventoryReportHandler>();

        services.AddScoped<GetShopSettingsHandler>();
        services.AddScoped<SaveShopSettingsHandler>();

        return services;
    }
}
