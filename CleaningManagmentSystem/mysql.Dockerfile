FROM mysql:8.0

# Set default environment (can be overridden)
ENV MYSQL_DATABASE=yeka_cleaning
ENV MYSQL_USER=yeka_user
ENV MYSQL_PASSWORD=yeka_pass123
ENV MYSQL_ROOT_PASSWORD=root_yeka_2026

# Custom MySQL config for better compatibility
RUN echo "[mysqld]\ncharacter-set-server=utf8mb4\ncollation-server=utf8mb4_unicode_ci\ndefault-authentication-plugin=mysql_native_password\nbind-address=0.0.0.0" > /etc/mysql/conf.d/custom.cnf

EXPOSE 3306
