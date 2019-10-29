﻿using EventFly.Core;

namespace EventFly.Permissions
{
    public interface ISecurityService
    {
        void Authorized(string userId);
        void HasPermissions(string userId, IIdentity targetObjectId, params  string[] permissionCodes);
        bool CheckPermissions(string userId, IIdentity targetObjectId, params  string[] permissionCodes);
    }
}