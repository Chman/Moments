/*
 * Copyright (c) 2015 Thomas Hourdel
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 *    1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 
 *    2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 
 *    3. This notice may not be removed or altered from any source
 *    distribution.
 */

using UnityEngine;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Moments
{
	public class ReflectionUtils<T> where T : class, new()
	{
		readonly T _Instance;

		public ReflectionUtils(T instance)
		{
			_Instance = instance;
		}

		public string GetFieldName<U>(Expression<Func<T, U>> fieldAccess)
		{
			MemberExpression memberExpression = fieldAccess.Body as MemberExpression;

			if (memberExpression != null)
				return memberExpression.Member.Name;

			throw new InvalidOperationException("Member expression expected");
		}

		public FieldInfo GetField(string fieldName)
		{
			return typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
		}

		public A GetAttribute<A>(FieldInfo field) where A : Attribute
		{
			return (A)Attribute.GetCustomAttribute(field, typeof(A));
		}

		// MinAttribute
		public void ConstrainMin<U>(Expression<Func<T, U>> fieldAccess, float value)
		{
			FieldInfo fieldInfo = GetField(GetFieldName(fieldAccess));
			fieldInfo.SetValue(_Instance, Mathf.Max(value, GetAttribute<MinAttribute>(fieldInfo).min));
		}

		public void ConstrainMin<U>(Expression<Func<T, U>> fieldAccess, int value)
		{
			FieldInfo fieldInfo = GetField(GetFieldName(fieldAccess));
			fieldInfo.SetValue(_Instance, (int)Mathf.Max(value, GetAttribute<MinAttribute>(fieldInfo).min));
		}

		// RangeAttribute
		public void ConstrainRange<U>(Expression<Func<T, U>> fieldAccess, float value)
		{
			FieldInfo fieldInfo = GetField(GetFieldName(fieldAccess));
			RangeAttribute attr = GetAttribute<RangeAttribute>(fieldInfo);
			fieldInfo.SetValue(_Instance, Mathf.Clamp(value, attr.min, attr.max));
		}

		public void ConstrainRange<U>(Expression<Func<T, U>> fieldAccess, int value)
		{
			FieldInfo fieldInfo = GetField(GetFieldName(fieldAccess));
			RangeAttribute attr = GetAttribute<RangeAttribute>(fieldInfo);
			fieldInfo.SetValue(_Instance, (int)Mathf.Clamp(value, attr.min, attr.max));
		}
	}
}
