// Geuneda.Core AppIdentity 의 iOS 키체인 저장소(IosKeychainStore.cs)
// ARC 여부와 관계없이 동작하도록 NSString 을 직접 만들지 않고 바이트를 복사한다
#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <stdlib.h>
#include <string.h>

static NSMutableDictionary* GeunedaKeychainQuery(const char* key)
{
    NSString* bundleId = [[NSBundle mainBundle] bundleIdentifier];
    NSString* service = [NSString stringWithFormat:@"%@.geuneda", bundleId != nil ? bundleId : @"unity"];

    NSMutableDictionary* query = [NSMutableDictionary dictionary];
    query[(__bridge id)kSecClass] = (__bridge id)kSecClassGenericPassword;
    query[(__bridge id)kSecAttrService] = service;
    query[(__bridge id)kSecAttrAccount] = [NSString stringWithUTF8String:key];
    return query;
}

// 값을 malloc 한 UTF-8 문자열로 돌려준다(IL2CPP 가 해제). 없거나 실패하면 NULL 이고 status 에 OSStatus 를 넣는다
extern "C" char* GeunedaKeychain_Copy(const char* key, int* status)
{
    @autoreleasepool
    {
        NSMutableDictionary* query = GeunedaKeychainQuery(key);
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;

        CFTypeRef result = NULL;
        OSStatus copyStatus = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        *status = (int)copyStatus;

        if (copyStatus != errSecSuccess || result == NULL)
        {
            return NULL;
        }

        CFDataRef data = (CFDataRef)result;
        CFIndex length = CFDataGetLength(data);
        char* value = (char*)malloc((size_t)length + 1);

        if (value != NULL)
        {
            memcpy(value, CFDataGetBytePtr(data), (size_t)length);
            value[length] = '\0';
        }

        CFRelease(result);
        return value;
    }
}

// 새 항목을 추가하고 OSStatus 를 돌려준다. 이미 있으면 errSecDuplicateItem
extern "C" int GeunedaKeychain_Add(const char* key, const char* value)
{
    @autoreleasepool
    {
        NSMutableDictionary* query = GeunedaKeychainQuery(key);
        query[(__bridge id)kSecValueData] = [NSData dataWithBytes:value length:strlen(value)];
        query[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
        return (int)SecItemAdd((__bridge CFDictionaryRef)query, NULL);
    }
}
