public interface IGuestIdentityProvider
{
    GuestLoginRequest CreateRequest();
    DeviceInfoDto CreateDeviceInfo();
}
